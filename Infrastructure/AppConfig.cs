using System.Text.Json;

namespace InterviewPrep.Infrastructure;

/// <summary>One selectable AI model/provider that speaks the OpenAI-compatible
/// chat-completions protocol (works for OpenAI/ChatGPT, Groq, and others).</summary>
public sealed record AiProvider(
    string Id,
    string DisplayName,
    string? ApiKey,
    string Model,
    string BaseUrl)
{
    /// <summary>True when this provider has an API key and can actually be used.</summary>
    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Minimal configuration loaded from appsettings.json + appsettings.Local.json
/// with environment-variable overrides. Supports several AI providers so the
/// user can switch models (e.g. Groq or OpenAI/ChatGPT) at runtime. Keeps config
/// dependency-free while keeping secrets outside source control. AI is optional.
/// </summary>
public sealed class AppConfig
{
    /// <summary>All configured providers, in display order.</summary>
    public IReadOnlyList<AiProvider> Providers { get; init; } = Array.Empty<AiProvider>();

    /// <summary>Id of the provider used when the caller does not choose one.</summary>
    public string DefaultProviderId { get; init; } = "groq";

    /// <summary>Providers that have an API key configured (usable right now).</summary>
    public IReadOnlyList<AiProvider> EnabledProviders =>
        Providers.Where(p => p.HasKey).ToList();

    /// <summary>True when at least one provider has a key.</summary>
    public bool HasAnyAi => Providers.Any(p => p.HasKey);

    /// <summary>Resolves a provider by id, preferring one that has a key. Falls
    /// back to the default provider, then the first usable one.</summary>
    public AiProvider GetProvider(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var byId = Providers.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (byId is { HasKey: true })
            {
                return byId;
            }
        }

        var def = Providers.FirstOrDefault(p =>
            string.Equals(p.Id, DefaultProviderId, StringComparison.OrdinalIgnoreCase));
        if (def is { HasKey: true })
        {
            return def;
        }

        return EnabledProviders.FirstOrDefault()
               ?? def
               ?? Providers.FirstOrDefault()
               ?? new AiProvider("none", "None", null, string.Empty, string.Empty);
    }

    // ---- Backward-compatible accessors for callers that don't switch models. ----
    // They target the resolved default/active provider.
    private AiProvider Active => GetProvider(DefaultProviderId);

    public string? OpenAiApiKey => Active.ApiKey;

    public string OpenAiModel => Active.Model;

    public string OpenAiBaseUrl => Active.BaseUrl;

    public bool HasOpenAi => HasAnyAi;

    public static AppConfig Load(string projectRoot)
    {
        // Built-in defaults so the app runs even with no settings files present.
        var byId = new Dictionary<string, AiProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["groq"] = new(
                "groq", "Groq \u00b7 Llama 3.3 70B (free)", null,
                "llama-3.3-70b-versatile", "https://api.groq.com/openai/v1/chat/completions"),
            ["groq-compound"] = new(
                "groq-compound", "Groq \u00b7 Compound (web-connected)", null,
                "groq/compound", "https://api.groq.com/openai/v1/chat/completions"),
            ["groq-gptoss"] = new(
                "groq-gptoss", "Groq \u00b7 GPT-OSS 120B (free)", null,
                "openai/gpt-oss-120b", "https://api.groq.com/openai/v1/chat/completions"),
            ["gemini"] = new(
                "gemini", "Google Gemini \u00b7 2.0 Flash (free)", null,
                "gemini-2.0-flash",
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"),
            ["openrouter"] = new(
                "openrouter", "OpenRouter \u00b7 Llama 3.3 70B (free)", null,
                "meta-llama/llama-3.3-70b-instruct:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["nvidia"] = new(
                "nvidia", "NVIDIA \u00b7 Nemotron Super 49B (best, fast)", null,
                "nvidia/llama-3.3-nemotron-super-49b-v1.5",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["nvidia-nano"] = new(
                "nvidia-nano", "NVIDIA \u00b7 Nemotron Nano 9B (fastest)", null,
                "nvidia/nvidia-nemotron-nano-9b-v2",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["nvidia-llama70b"] = new(
                "nvidia-llama70b", "NVIDIA \u00b7 Llama 3.1 70B (bigger, slower)", null,
                "meta/llama-3.1-70b-instruct",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["nvidia-deepseek"] = new(
                "nvidia-deepseek", "NVIDIA \u00b7 DeepSeek V4 Flash", null,
                "deepseek-ai/deepseek-v4-flash",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["ollama"] = new(
                "ollama", "Ollama \u00b7 Llama 3.1 (local, no key)", "ollama",
                "llama3.1", "http://localhost:11434/v1/chat/completions"),
            ["openai"] = new(
                "openai", "OpenAI \u00b7 GPT-4o mini (ChatGPT API)", null,
                "gpt-4o-mini", "https://api.openai.com/v1/chat/completions"),
        };
        var order = new List<string> { "groq", "groq-compound", "groq-gptoss", "gemini", "openrouter", "nvidia", "nvidia-nano", "nvidia-llama70b", "nvidia-deepseek", "ollama", "openai" };
        var defaultId = "groq";

        // Build the list of config files to read, in priority order (later files
        // win). We always read the files next to the app first, then a fixed
        // per-user folder (%USERPROFILE%\.krishnaagent). That lets the user set
        // their API key ONCE in the home folder and have every copy of the exe
        // — no matter where it lives — pick it up automatically. No copying, no
        // remembering the key each time.
        var searchPaths = new List<string>
        {
            Path.Combine(projectRoot, "appsettings.json"),
            Path.Combine(projectRoot, "appsettings.Local.json"),
        };
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                var homeDir = Path.Combine(home, ".krishnaagent");
                searchPaths.Add(Path.Combine(homeDir, "appsettings.json"));
                searchPaths.Add(Path.Combine(homeDir, "appsettings.Local.json"));
            }
        }
        catch
        {
            // Ignore if the home folder can't be resolved; app-local files still work.
        }

        foreach (var path in searchPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;

                // Preferred: multi-provider "AiProviders" section.
                if (root.TryGetProperty("AiProviders", out var ap))
                {
                    if (TryGetString(ap, "Default", out var d))
                    {
                        defaultId = d;
                    }

                    if (ap.TryGetProperty("Options", out var opts) &&
                        opts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var o in opts.EnumerateArray())
                        {
                            if (!TryGetString(o, "Id", out var id))
                            {
                                continue;
                            }

                            byId.TryGetValue(id, out var existing);
                            var display = existing?.DisplayName ?? id;
                            var model = existing?.Model ?? string.Empty;
                            var url = existing?.BaseUrl ?? string.Empty;
                            var key = existing?.ApiKey;

                            if (TryGetString(o, "DisplayName", out var dn)) display = dn;
                            if (TryGetString(o, "Model", out var m)) model = m;
                            if (TryGetString(o, "BaseUrl", out var b)) url = b;
                            if (TryGetString(o, "ApiKey", out var k)) key = k;

                            if (existing is null &&
                                !order.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                            {
                                order.Add(id);
                            }

                            byId[id] = new AiProvider(id, display, key, model, url);
                        }
                    }
                }

                // Legacy single "OpenAi" section (older config). Its key maps to
                // whichever provider matches its BaseUrl host; historically Groq.
                if (root.TryGetProperty("OpenAi", out var oa))
                {
                    var legacyUrl = TryGetString(oa, "BaseUrl", out var lu) ? lu : null;
                    var targetId =
                        legacyUrl is null ? "groq"
                        : legacyUrl.Contains("groq", StringComparison.OrdinalIgnoreCase) ? "groq"
                        : legacyUrl.Contains("openai", StringComparison.OrdinalIgnoreCase) ? "openai"
                        : defaultId;

                    byId.TryGetValue(targetId, out var ex);
                    var display = ex?.DisplayName ?? targetId;
                    var model = ex?.Model ?? string.Empty;
                    var url = ex?.BaseUrl ?? string.Empty;
                    var key = ex?.ApiKey;

                    if (TryGetString(oa, "Model", out var m2)) model = m2;
                    if (legacyUrl is not null) url = legacyUrl;
                    if (TryGetString(oa, "ApiKey", out var k2)) key = k2;

                    byId[targetId] = new AiProvider(targetId, display, key, model, url);
                }
            }
            catch (JsonException)
            {
                // Ignore a malformed settings file and fall back to defaults.
            }
        }

        // Environment-variable overrides (handy for hosting).
        ApplyEnvKey(byId, "groq", "GROQ_API_KEY");
        ApplyEnvKey(byId, "gemini", "GEMINI_API_KEY");
        ApplyEnvKey(byId, "openrouter", "OPENROUTER_API_KEY");
        ApplyEnvKey(byId, "nvidia", "NVIDIA_API_KEY");
        ApplyEnvKey(byId, "openai", "OPENAI_API_KEY");
        var legacyEnv = Environment.GetEnvironmentVariable("OpenAi__ApiKey");
        if (!string.IsNullOrWhiteSpace(legacyEnv) && byId.TryGetValue("groq", out var g))
        {
            byId["groq"] = g with { ApiKey = legacyEnv };
        }

        // One NVIDIA key powers every NVIDIA model. Share the "nvidia" key (or
        // the NVIDIA_API_KEY env var) with the other nvidia-* models so the user
        // only configures the key once.
        var nvidiaEnv = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
        var nvidiaKey = !string.IsNullOrWhiteSpace(nvidiaEnv)
            ? nvidiaEnv
            : (byId.TryGetValue("nvidia", out var nv) ? nv.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(nvidiaKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("nvidia", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = nvidiaKey };
                }
            }
        }

        // Likewise, one Groq key powers every Groq model (groq, groq-compound,
        // groq-gptoss). Share the "groq" key with the other groq-* models.
        var groqEnv = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        var groqKey = !string.IsNullOrWhiteSpace(groqEnv)
            ? groqEnv
            : (byId.TryGetValue("groq", out var gq) ? gq.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("groq", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = groqKey };
                }
            }
        }

        var providers = order
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();

        return new AppConfig { Providers = providers, DefaultProviderId = defaultId };
    }

    private static void ApplyEnvKey(Dictionary<string, AiProvider> map, string id, string envName)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(value) && map.TryGetValue(id, out var p))
        {
            map[id] = p with { ApiKey = value };
        }
    }

    private static bool TryGetString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (parent.TryGetProperty(name, out var el) &&
            el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                value = s;
                return true;
            }
        }

        return false;
    }
}
