using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Services;

/// <summary>
/// A CLI-style "agent mode" like Copilot CLI or Claude CLI: you name a new
/// project, choose where to put it (Desktop, C:\, anywhere), and describe what
/// it should do. The AI returns the FULL content of every file, and the agent
/// creates the new folder there and writes the whole project. The one folder it
/// will NEVER write into is this app's own project, so it can't break itself.
/// </summary>
public sealed class CodeAgent : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private readonly string _appProjectRoot;
    private readonly string _defaultBase;

    // A small on-disk memory so generations survive after you close the app.
    // The first run saves the AI's output to a JSON file; the next run — even a
    // brand-new session — reads it back and skips the server.
    private sealed record CachedFile(string Path, string Content);
    private sealed record CachedProject(string Message, List<CachedFile> Files);

    private static readonly string _cacheFile = Path.Combine(
        AppContext.BaseDirectory, "agent-cache.json");

    private static readonly ConcurrentDictionary<string, CachedProject> _cache = LoadCache();

    /// <summary>Loads the saved cache from disk, or starts empty if there isn't one.</summary>
    private static ConcurrentDictionary<string, CachedProject> LoadCache()
    {
        try
        {
            if (File.Exists(_cacheFile))
            {
                var json = File.ReadAllText(_cacheFile);
                var data = JsonSerializer.Deserialize<Dictionary<string, CachedProject>>(json);
                if (data is not null)
                {
                    return new ConcurrentDictionary<string, CachedProject>(data, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch { /* a bad or missing cache file just means we start fresh */ }

        return new ConcurrentDictionary<string, CachedProject>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Writes the whole cache back to its JSON file so it persists.</summary>
    private static void SaveCache()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cacheFile);
            if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFile, json);
        }
        catch { /* if we can't write the cache, we simply won't persist it */ }
    }

    /// <summary>Result of trying to write one file the agent proposed.</summary>
    public readonly record struct AgentFileResult(string Path, string Status, string Content = "");

    public CodeAgent(AppConfig config)
    {
        _config = config;
        // Bigger, enterprise-style apps take longer to generate, so allow a
        // generous timeout for the model to stream a large multi-file reply.
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

        // The app's OWN project folder — protected, never written into.
        _appProjectRoot = Path.GetFullPath(ProjectPaths.ProjectRoot);

        // Where projects go when the user doesn't type a location: the Desktop,
        // falling back to the user profile folder.
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _defaultBase = !string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop)
            ? desktop
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>The folder used when the user leaves the location box empty.</summary>
    public string DefaultBase => _defaultBase;

    /// <summary>
    /// Runs one agent task: creates a NEW project folder named <paramref name="projectName"/>
    /// under <paramref name="basePath"/> (or the Desktop by default), asks the AI for
    /// every file the project needs, then writes them into that folder. Returns the
    /// AI's short message, the per-file result list, a notice when something went
    /// wrong, the provider used, and the absolute path of the project folder.
    /// </summary>
    public async Task<(string message, IReadOnlyList<AgentFileResult> files, string? notice, string source, string projectFolder)> RunAsync(
        string task, string? projectName, string? basePath, string? providerId = null, CancellationToken ct = default)
    {
        task = (task ?? string.Empty).Trim();
        var folderName = SanitizeProjectName(projectName);

        // Work out where the new project folder should live.
        var baseDir = ResolveBase(basePath);
        var projectFolder = Path.GetFullPath(Path.Combine(baseDir, folderName));

        if (task.Length == 0)
        {
            return ("Name a project, pick a location, and describe what it should do.",
                Array.Empty<AgentFileResult>(), null, "info", projectFolder);
        }

        // Safety: never write into (or over) this app's own project folder.
        if (OverlapsAppProject(projectFolder))
        {
            return (string.Empty, Array.Empty<AgentFileResult>(),
                "\u26a0\ufe0f That location is inside this app's own project. Pick a different " +
                "folder (e.g. your Desktop) so the app can't be overwritten.", "blocked", projectFolder);
        }

        // Memory first: if we've already generated this exact task before (this
        // session OR a past one), reuse the saved copy and skip the server.
        var cacheKey = folderName + "|" + task;
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            var cachedFiles = cached.Files.Select(f => (f.Path, f.Content)).ToList();
            var cachedResults = WriteFiles(projectFolder, cachedFiles);
            return (cached.Message, cachedResults, null, "memory (saved)", projectFolder);
        }

        // Try the requested provider first, then any other provider that has a key.
        var tryOrder = new List<AiProvider>();
        var requested = _config.GetProvider(providerId);
        if (requested.HasKey)
        {
            tryOrder.Add(requested);
        }

        foreach (var p in _config.EnabledProviders)
        {
            if (!tryOrder.Any(x => x.Id == p.Id))
            {
                tryOrder.Add(p);
            }
        }

        if (tryOrder.Count == 0)
        {
            return (string.Empty, Array.Empty<AgentFileResult>(),
                "\u26a0\ufe0f No AI key is set, so the agent cannot run. Add a free key in " +
                "appsettings.Local.json and restart.", "study bank", projectFolder);
        }

        var system =
            "You are a senior software engineer and coding agent like Copilot CLI or Claude CLI. You " +
            "SCAFFOLD a brand-new, self-contained, PRODUCTION-QUALITY project from scratch based on the " +
            "user's request. You MUST reply with " +
            "ONLY a single JSON object \u2014 no prose, no markdown, no code fences. The JSON shape is EXACTLY:\n" +
            "{ \"message\": \"one short sentence describing the project you created\", " +
            "\"files\": [ { \"path\": \"relative/path/inside/the/project.ext\", \"content\": \"the FULL file content\" } ] }\n" +
            "STRICT RULES:\n" +
            "- Create EVERY file the project needs to build and run: source files, the project/manifest " +
            "file (e.g. .csproj, package.json), and a short README.md with run instructions.\n" +
            "- Output the COMPLETE final content of each file, never a diff or a partial file.\n" +
            "- Paths are RELATIVE to the new project folder, with forward slashes. Do NOT include the " +
            "project folder name itself, and never use an absolute path, a drive letter, a leading slash, or '..'.\n" +
            "- Pick a sensible tech stack for the request (default to C# .NET 8 unless the user asks otherwise).\n" +
            "- Write correct, compilable, idiomatic, production-grade code. Keep the file list focused but complete.\n" +
            "- BUILD LIKE A REAL ENGINEER, not a toy. For anything beyond a trivial script, use a clean, " +
            "layered structure and separate files by responsibility: e.g. Models/entities, Services/business " +
            "logic, Data/repositories or storage, and the app entry point. Use dependency injection, interfaces, " +
            "async where appropriate, input validation, and proper error handling. Add configuration files " +
            "(appsettings.json, .env.example, etc.) when they fit. Add a .gitignore.\n" +
            "- For web/API requests, include real endpoints, a layered structure, and DTOs/models. For apps " +
            "with data, use a simple persistent store (in-memory or a local file/SQLite) with a repository layer.\n" +
            "- Where it adds value and stays within the size budget, include a small unit-test project or a few " +
            "tests so the app is verifiable.\n" +
            "- Aim for a realistic, enterprise-style layout that a developer could extend \u2014 but keep every " +
            "file complete and the whole project buildable. Prefer a well-structured app of many small, correct " +
            "files over one giant file.\n" +
            "- The user may mis-spell words or use broken/short English. Do NOT copy the typos. " +
            "Figure out what they REALLY mean and build that. For example 'creat calclator ap' means " +
            "'create a calculator app', 'tik tak toe' means 'tic-tac-toe game', 'weathr' means 'weather'. " +
            "Silently correct spelling and grammar, infer the intended project, and scaffold the correct thing. " +
            "Only if the request is truly impossible to guess, make a reasonable simple choice and note it in the message.\n" +
            "- Return ONLY the JSON object.";

        var userMsg = $"Project folder name: {folderName}\nTask: {task}";

        string? lastNotice = null;
        foreach (var p in tryOrder)
        {
            var (content, notice) = await PostAsync(p, system, userMsg, ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                if (TryParse(content!, out var message, out var files))
                {
                    // Remember this generation on disk so the next identical task
                    // (even in a new session) is served from memory, not the server.
                    _cache[cacheKey] = new CachedProject(
                        message, files.Select(f => new CachedFile(f.Path, f.Content)).ToList());
                    SaveCache();
                    var results = WriteFiles(projectFolder, files);
                    return (message, results, null, p.DisplayName, projectFolder);
                }

                lastNotice = "\u26a0\ufe0f The AI reply was not valid JSON, so nothing was written. Try again " +
                             "or rephrase the task.";
                continue;
            }

            lastNotice = notice ?? lastNotice;
        }

        return (string.Empty, Array.Empty<AgentFileResult>(),
            lastNotice ?? "\u26a0\ufe0f The AI model could not be reached. Check your connection and try again.",
            "study bank", projectFolder);
    }

    /// <summary>Calls one provider's chat-completions endpoint and returns the raw
    /// assistant text, or a short notice describing why it failed.</summary>
    private async Task<(string? content, string? notice)> PostAsync(
        AiProvider provider, string system, string user, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                model = provider.Model,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                temperature = 0.1,
                // Large-but-safe budget: big enough for a complete multi-layer
                // app, but within the per-request completion limit that every
                // provider (incl. NVIDIA NIM) accepts, so we don't get a 400 and
                // fall back to a weaker model.
                max_tokens = 8000,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, provider.BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var why = (int)response.StatusCode switch
                {
                    429 => $"\u26a0\ufe0f {provider.DisplayName} hit its rate limit or daily quota.",
                    401 or 403 => $"\u26a0\ufe0f {provider.DisplayName}\u2019s API key is missing or invalid.",
                    _ => $"\u26a0\ufe0f {provider.DisplayName} returned an error ({(int)response.StatusCode}).",
                };
                return (null, why);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return (content, null);
        }
        catch (TaskCanceledException)
        {
            return (null, $"\u26a0\ufe0f {provider.DisplayName} timed out. Check your internet connection.");
        }
        catch (Exception ex)
        {
            return (null, $"\u26a0\ufe0f Could not reach {provider.DisplayName}: {ex.Message}");
        }
    }

    /// <summary>Extracts the message and the list of (path, content) files from the
    /// model's JSON reply. Tolerates surrounding text or code fences.</summary>
    private static bool TryParse(
        string raw, out string message, out List<(string Path, string Content)> files)
    {
        message = string.Empty;
        files = new List<(string, string)>();

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw.Substring(start, end - start + 1));
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            {
                message = m.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("files", out var fs) && fs.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in fs.EnumerateArray())
                {
                    var path = el.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString() : null;
                    var content = el.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(path) && content is not null)
                    {
                        files.Add((path!, content));
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return files.Count > 0 || !string.IsNullOrWhiteSpace(message);
    }

    /// <summary>Writes each file into the given project folder, when its path safely
    /// resolves inside that folder. Anything that tries to escape is blocked.</summary>
    private static IReadOnlyList<AgentFileResult> WriteFiles(
        string projectFolder, List<(string Path, string Content)> files)
    {
        var results = new List<AgentFileResult>();
        foreach (var (relPath, content) in files)
        {
            if (!TryResolveSafe(projectFolder, relPath, out var full))
            {
                results.Add(new AgentFileResult(relPath, "blocked (unsafe path)"));
                continue;
            }

            try
            {
                var existed = File.Exists(full);
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(full, content);
                var shown = Path.GetRelativePath(projectFolder, full).Replace('\\', '/');
                results.Add(new AgentFileResult(shown, existed ? "updated" : "created", content));
            }
            catch (Exception ex)
            {
                results.Add(new AgentFileResult(relPath, "error: " + ex.Message));
            }
        }

        return results;
    }

    /// <summary>Resolves a model-supplied relative path to a full path and confirms
    /// it stays inside the project folder. Rejects absolute paths and '..' traversal.</summary>
    private static bool TryResolveSafe(string projectFolder, string rel, out string full)
    {
        full = string.Empty;
        if (string.IsNullOrWhiteSpace(rel))
        {
            return false;
        }

        rel = rel.Replace('\\', '/').Trim().TrimStart('/');
        if (rel.Length == 0 || rel.Contains("..") || Path.IsPathRooted(rel))
        {
            return false;
        }

        var root = Path.GetFullPath(projectFolder);
        var combined = Path.GetFullPath(Path.Combine(root, rel));
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        full = combined;
        return true;
    }

    /// <summary>Turns a user-supplied project name into a single safe folder name.
    /// Strips path separators and unsafe characters; falls back to a default.</summary>
    private static string SanitizeProjectName(string? name)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "NewProject";
        }

        // Keep only the last segment and drop anything that isn't a safe char.
        name = name.Replace('\\', '/');
        name = name.Substring(name.LastIndexOf('/') + 1);

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            {
                sb.Append(ch);
            }
            else if (ch == ' ')
            {
                sb.Append('-');
            }
        }

        var cleaned = sb.ToString().Trim('.', '-', '_');
        return cleaned.Length == 0 ? "NewProject" : cleaned;
    }

    /// <summary>Works out the base folder new projects go under. Accepts any
    /// absolute path the user types (Desktop, C:\, etc.); falls back to the
    /// default Desktop folder when empty or not a valid rooted path.</summary>
    private string ResolveBase(string? basePath)
    {
        basePath = (basePath ?? string.Empty).Trim().Trim('"');
        if (basePath.Length == 0 || !Path.IsPathRooted(basePath))
        {
            return _defaultBase;
        }

        try
        {
            return Path.GetFullPath(basePath);
        }
        catch
        {
            return _defaultBase;
        }
    }

    /// <summary>True when the target project folder is the same as, inside, or a
    /// parent of this app's own project folder — which we must never write into.</summary>
    private bool OverlapsAppProject(string projectFolder)
    {
        var target = Path.GetFullPath(projectFolder).TrimEnd(Path.DirectorySeparatorChar);
        var app = _appProjectRoot.TrimEnd(Path.DirectorySeparatorChar);

        if (string.Equals(target, app, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var targetSep = target + Path.DirectorySeparatorChar;
        var appSep = app + Path.DirectorySeparatorChar;
        return target.StartsWith(appSep, StringComparison.OrdinalIgnoreCase)
            || app.StartsWith(targetSep, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _http.Dispose();
}
