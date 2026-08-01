using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Sockets;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;
using InterviewPrep.Models;
using InterviewPrep.Services;
using InterviewPrep.Web;

// Interview Practice — an honest study tool. Pick a topic, get a real technical
// question, answer it in your own words, and get instant feedback plus a strong
// model answer so you actually LEARN the material.
//
// Modes:
//   (default)   interactive console practice.
//   --web       practice dashboard at http://localhost:5095.
//   --agent     CLI coding agent: scaffold a whole new project from the terminal.

var config = AppConfig.Load(ProjectPaths.ProjectRoot);
var scorer = new AnswerScorer();

if (HasFlag(args, "--web", "web"))
{
    RunWeb(args, config, scorer);
    return;
}

if (HasFlag(args, "--agent", "agent", "--cli", "cli"))
{
    await RunAgentCliAsync(config);
    return;
}

await RunConsoleAsync(config, scorer);
return;

static bool HasFlag(string[] args, params string[] names) =>
    args.Any(a => names.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)));

// A real command-line coding agent, like Copilot CLI: type a description to
// scaffold a whole new project on disk, or use slash commands (/help, /model,
// /models, /clear, /cwd, /env, /usage, /version, /exit). Never writes into this
// app's own project folder.
static async Task RunAgentCliAsync(AppConfig config)
{
    using var agent = new CodeAgent(config);

    // Session state (like Copilot CLI: model and working directory can change
    // during the session without touching saved settings).
    string? sessionModelId = null;
    var sessionLocation = agent.DefaultBase;
    var history = new List<string>();
    var created = 0;
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "1.0";

    try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* some terminals disallow this */ }

    PrintBanner();

    while (true)
    {
        var prompt = "\U0001F916 Krishnaagent > ";
        Console.Write(prompt);
        var input = ReadLineOrEscape(prompt, history);
        if (input is null)
        {
            break; // Esc pressed
        }

        input = input.Trim();
        if (input.Length == 0)
        {
            continue;
        }

        // Remember this line so the Up/Down arrows can recall it later.
        if (history.Count == 0 || !string.Equals(history[^1], input, StringComparison.Ordinal))
        {
            history.Add(input);
        }

        // Quick help, like Copilot CLI's `?`.
        if (input == "?")
        {
            PrintHelp();
            continue;
        }

        // Slash commands.
        if (input.StartsWith('/'))
        {
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();
            var rest = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            switch (cmd)
            {
                case "/exit":
                case "/quit":
                    Console.WriteLine("Bye.");
                    return;

                case "/help":
                    PrintHelp();
                    break;

                case "/clear":
                case "/new":
                    Console.Clear();
                    PrintBanner();
                    break;

                case "/version":
                    Console.WriteLine($"Krishnaagent v{version}\n");
                    break;

                case "/model":
                    if (rest.Length == 0)
                    {
                        Console.WriteLine($"Current model: {CurrentModelName()}");
                        Console.WriteLine("Change it with: /model <id>   (see /models)\n");
                    }
                    else
                    {
                        var match = config.Providers.FirstOrDefault(p =>
                            string.Equals(p.Id, rest, StringComparison.OrdinalIgnoreCase));
                        if (match is null)
                        {
                            Console.WriteLine($"No model with id '{rest}'. See /models.\n");
                        }
                        else if (!match.HasKey)
                        {
                            Console.WriteLine($"'{match.Id}' has no API key. Add one in appsettings.Local.json first.\n");
                        }
                        else
                        {
                            sessionModelId = match.Id;
                            Console.WriteLine($"Model set to: {match.DisplayName}\n");
                        }
                    }
                    break;

                case "/models":
                    Console.WriteLine("Available models:");
                    foreach (var p in config.Providers)
                    {
                        var activeMark = string.Equals(p.Id, CurrentProvider().Id, StringComparison.OrdinalIgnoreCase) ? "*" : " ";
                        var keyMark = p.HasKey ? "" : "  (no key)";
                        Console.WriteLine($"  {activeMark} {p.Id,-16} {p.DisplayName}{keyMark}");
                    }
                    Console.WriteLine();
                    break;

                case "/cwd":
                case "/cd":
                    if (rest.Length == 0)
                    {
                        Console.WriteLine($"Location: {sessionLocation}\n");
                    }
                    else
                    {
                        sessionLocation = rest;
                        Console.WriteLine($"Location set to: {sessionLocation}\n");
                    }
                    break;

                case "/env":
                    Console.WriteLine($"  Model    : {CurrentModelName()}");
                    Console.WriteLine($"  AI       : {(config.HasAnyAi ? "on" : "off")}");
                    Console.WriteLine($"  Location : {sessionLocation}");
                    Console.WriteLine($"  Models   : {config.Providers.Count(p => p.HasKey)} with a key / {config.Providers.Count} total");
                    Console.WriteLine($"  Version  : {version}\n");
                    break;

                case "/usage":
                    Console.WriteLine($"Projects created this session: {created}\n");
                    break;

                default:
                    Console.WriteLine($"Unknown command '{cmd}'. Type /help.\n");
                    break;
            }

            continue;
        }

        // Anything else is a project description. The agent decides the language,
        // the project name, and the folder itself — the user isn't asked for them.
        var task = input;
        var name = AutoProjectName(task);
        var location = sessionLocation;

        Console.WriteLine($"Project name (auto): {name}");
        Console.WriteLine($"Location (auto)    : {location}");
        Console.WriteLine("Working\u2026");
        var (message, files, notice, source, projectFolder) =
            await agent.RunAsync(task, name, location, sessionModelId);

        Console.WriteLine();
        if (!string.IsNullOrWhiteSpace(notice))
        {
            Console.WriteLine(notice);
        }

        if (files.Count > 0)
        {
            Console.WriteLine($"[{source}] {message}");
            Console.WriteLine($"Folder: {projectFolder}");
            foreach (var f in files)
            {
                Console.WriteLine($"  {f.Status,-22} {f.Path}");
                PrintFileDiff(f);
            }

            await VerifyProjectAsync(projectFolder, files);
            Console.WriteLine("Done.");
            created++;
        }
        else if (string.IsNullOrWhiteSpace(notice))
        {
            Console.WriteLine("No files were created.");
        }

        Console.WriteLine();
    }

    Console.WriteLine("Bye.");

    // ---- local helpers ----

    AiProvider CurrentProvider() => config.GetProvider(sessionModelId);

    string CurrentModelName()
    {
        var p = CurrentProvider();
        return p.HasKey ? p.DisplayName : "no model (add an API key)";
    }

    void PrintBanner()
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("  \U0001F916 Krishnaagent");
        Console.WriteLine("  your CLI coding agent \u2014 builds a whole new project for you");
        Console.ForegroundColor = prev;
        Console.WriteLine();
        Console.WriteLine($"  Model    : {CurrentModelName()}");
        Console.WriteLine(config.HasAnyAi
            ? "  AI       : on \u2014 describe a project and it will be created on disk."
            : "  AI       : OFF \u2014 add a free API key in appsettings.Local.json first.");
        Console.WriteLine($"  Location : {sessionLocation}");
        Console.WriteLine("  Tip      : type a project description, or /help for commands. Esc to exit.");
        Console.WriteLine();
    }

    static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  <description>   Describe a project to scaffold it on disk.");
        Console.WriteLine("  /help, ?        Show this help.");
        Console.WriteLine("  /model [id]     Show or change the AI model for this session.");
        Console.WriteLine("  /models         List all available models.");
        Console.WriteLine("  /cwd, /cd [dir] Show or change the default location.");
        Console.WriteLine("  /env            Show model, AI status, location, and version.");
        Console.WriteLine("  /usage          Projects created this session.");
        Console.WriteLine("  /clear, /new    Clear the screen.");
        Console.WriteLine("  /version        Show the version.");
        Console.WriteLine("  /exit, /quit    Leave (or press Esc).");
        Console.WriteLine();
    }

    // Builds a project name from the first words of the description, so the user
    // doesn't have to invent one. Deterministic, so the same request re-uses the
    // same saved project.
    static string AutoProjectName(string task)
    {
        var words = (task ?? string.Empty).Split(
            new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '/', '\\', '"', '\'' },
            StringSplitOptions.RemoveEmptyEntries);

        var sb = new System.Text.StringBuilder();
        var used = 0;
        foreach (var w in words)
        {
            var clean = new string(w.Where(char.IsLetterOrDigit).ToArray());
            if (clean.Length == 0) { continue; }
            if (sb.Length > 0) { sb.Append('-'); }
            sb.Append(char.ToUpperInvariant(clean[0]));
            if (clean.Length > 1) { sb.Append(clean.Substring(1).ToLowerInvariant()); }
            if (++used >= 4) { break; }
        }

        var name = sb.ToString().Trim('-');
        return name.Length == 0 ? "NewProject" : name;
    }

    // After a project is written, confirm every file is really on disk, then try
    // to run it in a child process as a quick verification.
    static async Task VerifyProjectAsync(string projectFolder, IReadOnlyList<CodeAgent.AgentFileResult> files)
    {
        Console.WriteLine();
        Console.WriteLine("Checking files on disk\u2026");
        var missing = 0;
        foreach (var f in files)
        {
            var full = Path.Combine(projectFolder, f.Path);
            var ok = File.Exists(full);
            Console.WriteLine($"  {(ok ? "OK     " : "MISSING")} {f.Path}");
            if (!ok) { missing++; }
        }

        if (missing > 0)
        {
            Console.WriteLine($"{missing} file(s) missing \u2014 skipping the run.");
            return;
        }
        Console.WriteLine("All files are present.");

        var (exe, args) = PickRunCommand(projectFolder, files);
        if (exe is null)
        {
            Console.WriteLine("No known way to run this project type \u2014 skipping the run step.");
            return;
        }

        Console.WriteLine($"Running to verify: {exe} {args}");
        Console.WriteLine("(stops when it finishes, or after 25s if it keeps running)\n");
        await RunForVerificationAsync(exe, args, projectFolder);
    }

    // Picks a run command based on the files present: .NET, Node, or Python.
    static (string? exe, string args) PickRunCommand(
        string folder, IReadOnlyList<CodeAgent.AgentFileResult> files)
    {
        bool Has(string fileName) => files.Any(f =>
            string.Equals(Path.GetFileName(f.Path), fileName, StringComparison.OrdinalIgnoreCase));

        var csproj = files.FirstOrDefault(f =>
            f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Path;
        if (!string.IsNullOrEmpty(csproj))
        {
            return ("dotnet", $"run --project \"{Path.Combine(folder, csproj)}\"");
        }

        if (Has("package.json"))
        {
            var entry = new[] { "index.js", "app.js", "server.js", "main.js" }
                .FirstOrDefault(Has);
            return entry is not null
                ? ("node", $"\"{Path.Combine(folder, entry)}\"")
                : ((string?)null, string.Empty);
        }

        var py = new[] { "main.py", "app.py" }.FirstOrDefault(Has)
            ?? files.FirstOrDefault(f =>
                f.Path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)).Path;
        if (!string.IsNullOrEmpty(py))
        {
            return ("python", $"\"{Path.Combine(folder, py)}\"");
        }

        return (null, string.Empty);
    }

    // Runs the child process, streams its output, and stops it after 25s so a
    // long-running server doesn't block the agent.
    static async Task RunForVerificationAsync(string exe, string args, string workDir)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            var sb = new System.Text.StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) { sb.AppendLine(e.Data); } };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) { sb.AppendLine(e.Data); } };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var exited = await Task.Run(() => proc.WaitForExit(25000));
            if (!exited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                Console.WriteLine(TrimOutput(sb.ToString()));
                Console.WriteLine("\nApp launched and kept running (looks like a server). " +
                    "Verified it starts; stopped it after 25s.");
                return;
            }

            Console.WriteLine(TrimOutput(sb.ToString()));
            Console.WriteLine(proc.ExitCode == 0
                ? "\nVerification run finished successfully (exit code 0)."
                : $"\nVerification run exited with code {proc.ExitCode}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not run it: {ex.Message}");
            Console.WriteLine("(The needed tool may not be installed \u2014 e.g. .NET, Node, or Python.)");
        }
    }

    // Keeps console output readable by trimming very long run logs.
    static string TrimOutput(string text)
    {
        text = text.TrimEnd();
        const int max = 4000;
        return text.Length <= max ? text : "\u2026" + text.Substring(text.Length - max);
    }

    // Prints a created/updated file's contents like Copilot CLI: each line
    // numbered with a green "+", so you can watch the code being written.
    static void PrintFileDiff(CodeAgent.AgentFileResult f)
    {
        if (string.IsNullOrEmpty(f.Content))
        {
            return;
        }

        var prev = Console.ForegroundColor;
        var lines = f.Content.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{i + 1,4} ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"+ {lines[i]}");
        }
        Console.ForegroundColor = prev;
        Console.WriteLine();
    }

    // Reads a line but returns null the moment Esc is pressed, so the user can
    // leave from any prompt. Ctrl+L clears the screen. Falls back to ReadLine
    // when input is piped.
    static string? ReadLineOrEscape(string prompt, List<string>? history = null)
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var sb = new System.Text.StringBuilder();
        var historyPos = history?.Count ?? 0;

        void Redraw()
        {
            try { Console.Write("\r" + new string(' ', Math.Max(0, Console.WindowWidth - 1)) + "\r"); }
            catch { Console.Write("\r"); }
            Console.Write(prompt);
            Console.Write(sb.ToString());
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return null;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return sb.ToString();
            }
            // Up/Down walk through earlier commands, like a normal shell.
            if (key.Key == ConsoleKey.UpArrow)
            {
                if (history is { Count: > 0 } && historyPos > 0)
                {
                    historyPos--;
                    sb.Clear();
                    sb.Append(history[historyPos]);
                    Redraw();
                }
                continue;
            }
            if (key.Key == ConsoleKey.DownArrow)
            {
                if (history is { Count: > 0 })
                {
                    if (historyPos < history.Count - 1)
                    {
                        historyPos++;
                        sb.Clear();
                        sb.Append(history[historyPos]);
                    }
                    else
                    {
                        historyPos = history.Count;
                        sb.Clear();
                    }
                    Redraw();
                }
                continue;
            }
            // Ctrl+L clears the screen and redraws the current line.
            if (key.Key == ConsoleKey.L && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                Console.Clear();
                Console.Write(prompt);
                Console.Write(sb.ToString());
                continue;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }
            if (!char.IsControl(key.KeyChar))
            {
                sb.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }
}


static async Task RunConsoleAsync(AppConfig config, AnswerScorer scorer)
{
    using var coach = new OpenAiCoach(config);
    var rng = new Random();

    Console.WriteLine("=== Interview Practice ===");
    Console.WriteLine($"AI coach: {(config.HasOpenAi ? "on" : "off")}");
    Console.WriteLine();
    Console.WriteLine("Topics: " + string.Join(", ", QuestionBank.Topics));
    Console.Write("Pick a topic (or press Enter for random, 'quit' to exit): ");
    var topic = Console.ReadLine()?.Trim();

    while (true)
    {
        if (string.Equals(topic, "quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        var pool = string.IsNullOrWhiteSpace(topic)
            ? QuestionBank.Questions
            : QuestionBank.ForTopic(topic);

        if (pool.Count == 0)
        {
            Console.WriteLine("No questions for that topic. Try: " + string.Join(", ", QuestionBank.Topics));
            Console.Write("Pick a topic: ");
            topic = Console.ReadLine()?.Trim();
            continue;
        }

        var q = pool[rng.Next(pool.Count)];
        Console.WriteLine();
        Console.WriteLine($"[{q.Topic} \u00b7 {q.Level}] {q.Prompt}");
        Console.Write("Your answer: ");
        var answer = Console.ReadLine() ?? string.Empty;

        var fb = scorer.Score(q, answer);
        Console.WriteLine();
        Console.WriteLine($"Score: {fb.ScorePercent}%  -  {fb.Comment}");
        if (fb.CoveredPoints.Count > 0)
        {
            Console.WriteLine("  You mentioned : " + string.Join(", ", fb.CoveredPoints));
        }

        if (fb.MissedPoints.Count > 0)
        {
            Console.WriteLine("  Add next time : " + string.Join(", ", fb.MissedPoints));
        }

        var note = await coach.CritiqueAsync(q, answer);
        if (!string.IsNullOrWhiteSpace(note))
        {
            Console.WriteLine("  Coach         : " + note);
        }

        Console.WriteLine();
        Console.WriteLine("Model answer:");
        Console.WriteLine("  " + q.ModelAnswer);
        Console.WriteLine();

        Console.Write("Enter for another, new topic name, or 'quit': ");
        var next = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(next))
        {
            topic = next;
        }
    }

    Console.WriteLine("Great work. Keep practicing!");
}

static string GetSessionId(HttpContext ctx)
{
    // Each browser gets its own random id in a cookie, so every visitor has a
    // separate chat history. Two people on the site at once never mix questions.
    const string cookie = "sid";
    if (ctx.Request.Cookies.TryGetValue(cookie, out var id) && !string.IsNullOrWhiteSpace(id))
    {
        return id;
    }

    id = Guid.NewGuid().ToString("N");
    ctx.Response.Cookies.Append(cookie, id, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromDays(7),
    });
    return id;
}

static void RunWeb(string[] args, AppConfig config, AnswerScorer scorer)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = ProjectPaths.ProjectRoot,
    });

    var app = builder.Build();
    var rng = new Random();

    app.MapGet("/", () => Results.Redirect("/intro"));

    // Landing page: a polished self-introduction to read, rehearse, and copy.
    app.MapGet("/intro", () => Results.Content(IntroPage.Render(), "text/html"));

    // Ask & Learn: type any technical question, get an explained answer to study.
    app.MapGet("/ask", (HttpContext ctx) =>
    {
        var sid = GetSessionId(ctx);
        return Results.Content(
            AskPage.Render(null, null, null, config.HasOpenAi,
                config.Providers, config.GetProvider(null).Id, null, null, sid),
            "text/html");
    });

    app.MapPost("/ask", async (HttpContext ctx) =>
    {
        var sid = GetSessionId(ctx);
        var form = await ctx.Request.ReadFormAsync();
        var question = form["question"].ToString();
        var model = form["model"].ToString();

        using var assistant = new StudyAssistant(config);
        var (answer, source, notice, usage) = await assistant.AnswerAsync(question, model, sid);

        var selected = config.GetProvider(model).Id;
        var html = AskPage.Render(question, answer, source, config.HasOpenAi,
            config.Providers, selected, notice, usage, sid);
        return Results.Content(html, "text/html");
    });

    // JSON answer endpoint — lets the private floating window ask a new question
    // and show the answer inside itself, without switching back to the shared tab.
    app.MapPost("/ask-json", async (HttpContext ctx) =>
    {
        var sid = GetSessionId(ctx);
        var form = await ctx.Request.ReadFormAsync();
        var question = form["question"].ToString();
        var model = form["model"].ToString();

        using var assistant = new StudyAssistant(config);
        var (answer, source, notice, usage) = await assistant.AnswerAsync(question, model, sid);
        return Results.Json(new { answer, source, notice, usage, html = AnswerFormat.ToHtml(answer) });
    });

    // Start a fresh topic — forget the running chat so the next question has no
    // earlier context. Used by the "New topic" button on the Ask page.
    app.MapPost("/ask/reset", (HttpContext ctx) =>
    {
        var sid = GetSessionId(ctx);
        StudyAssistant.ClearConversation(sid);
        return Results.Redirect("/ask");
    });

    // Agent mode: name a project, choose where to create it, and describe it.
    // The AI scaffolds a whole new project into a new folder anywhere you pick
    // (Desktop, C:\, etc.) but never touches this app. Like a CLI coding agent.
    app.MapGet("/agent", () =>
        Results.Content(
            AgentPage.Render(null, null, null, null, null,
                config.Providers, config.GetProvider(null).Id),
            "text/html"));

    app.MapPost("/agent", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var task = form["task"].ToString();
        var project = form["project"].ToString();
        var location = form["location"].ToString();
        var model = form["model"].ToString();

        using var agent = new CodeAgent(config);
        var (message, files, notice, source, projectFolder) = await agent.RunAsync(task, project, location, model);

        var selected = config.GetProvider(model).Id;
        var html = AgentPage.Render(task, project, location, message, files,
            config.Providers, selected, notice, source, projectFolder);
        return Results.Content(html, "text/html");
    });

    // Download the compiled agent as a single .exe. Anyone can save it and run it
    // in cmd with:  Krishnaagent.exe --agent   (no repo, no .NET install needed).
    app.MapGet("/download-agent", () =>
    {
        var exePath = Path.Combine(ProjectPaths.ProjectRoot, "downloads", "Krishnaagent.exe");
        return File.Exists(exePath)
            ? Results.File(exePath, "application/octet-stream", "Krishnaagent.exe")
            : Results.NotFound("The agent .exe has not been published yet. Ask the owner to build it.");
    });

    // Mock interview: answer a question, get coached, then face a follow-up.
    app.MapGet("/mock", (HttpRequest request) =>
    {
        var topic = request.Query["topic"].ToString();
        var model = request.Query["model"].ToString();

        // Opening a fresh mock (or switching topic) starts a brand-new interview,
        // so the AI interviewer's memory does not carry over from a past session.
        MockInterview.ResetInterview();

        using var mock = new MockInterview(config);
        var question = mock.FirstQuestion(topic);
        var shownTopic = string.IsNullOrWhiteSpace(topic) ? null : topic;
        return Results.Content(
            MockPage.Render(shownTopic, question, turn: null, config.HasOpenAi,
                config.Providers, config.GetProvider(model).Id),
            "text/html");
    });

    app.MapPost("/mock", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var topic = form["topic"].ToString();
        var question = form["question"].ToString();
        var answer = form["answer"].ToString();
        var model = form["model"].ToString();

        using var mock = new MockInterview(config);
        var turn = await mock.NextAsync(topic, question, answer, model);
        var shownTopic = string.IsNullOrWhiteSpace(topic) ? null : topic;

        // The follow-up becomes the next question to answer.
        var selected = config.GetProvider(model).Id;
        var html = MockPage.Render(shownTopic, turn.FollowUp, turn, config.HasOpenAi,
            config.Providers, selected);
        return Results.Content(html, "text/html");
    });

    // Live interview: webcam on, the app reads the rules and question out loud,
    // listens to your spoken answer, then says selected/rejected + how to improve.
    app.MapGet("/live", (HttpRequest request) =>
    {
        var topic = request.Query["topic"].ToString();
        var model = request.Query["model"].ToString();

        using var live = new LiveInterview(config);
        var question = live.NextQuestion(topic);
        var shownTopic = string.IsNullOrWhiteSpace(topic) ? null : topic;
        return Results.Content(
            LivePage.Render(shownTopic, question, config.HasOpenAi,
                config.Providers, config.GetProvider(model).Id),
            "text/html");
    });

    // Give the page a fresh spoken-interview question (used by "Next question").
    app.MapGet("/live/question", (HttpRequest request) =>
    {
        var topic = request.Query["topic"].ToString();
        using var live = new LiveInterview(config);
        return Results.Json(new { question = live.NextQuestion(topic) });
    });

    // Judge one spoken answer: return the verdict, drawbacks and improvements.
    app.MapPost("/live-json", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var topic = form["topic"].ToString();
        var question = form["question"].ToString();
        var answer = form["answer"].ToString();
        var model = form["model"].ToString();

        using var live = new LiveInterview(config);
        var v = await live.EvaluateAsync(topic, question, answer, model);
        return Results.Json(new
        {
            verdict = v.Verdict,
            score = v.Score,
            feedback = v.Feedback,
            drawbacks = v.Drawbacks,
            improve = v.Improve,
            modelAnswer = v.ModelAnswer,
        });
    });

    // Interview mode: a full resume-driven mock with 2 technical rounds, one
    // managerial round and one HR round. Questions are generated from the
    // candidate's own resume and tech stack.
    app.MapGet("/interview", (HttpRequest request) =>
    {
        var model = request.Query["model"].ToString();
        return Results.Content(
            InterviewPage.Render(config.HasOpenAi, config.Providers, config.GetProvider(model).Id),
            "text/html");
    });

    // Progress dashboard (all client-side; reads localStorage history).
    app.MapGet("/dashboard", () => Results.Content(DashboardPage.Render(), "text/html"));

    // Give the page the next question for a round (avoids ones already asked).
    app.MapPost("/interview/question", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var resume = form["resume"].ToString();
        var stack = form["stack"].ToString();
        var round = form["round"].ToString();
        var model = form["model"].ToString();
        var asked = form["asked"].ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        using var interview = new ResumeInterview(config);
        var question = await interview.NextQuestionAsync(resume, stack, round, asked, model);
        return Results.Json(new { question });
    });

    // Score one answer for a round: return score, feedback and short tips.
    app.MapPost("/interview/evaluate", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var resume = form["resume"].ToString();
        var stack = form["stack"].ToString();
        var round = form["round"].ToString();
        var question = form["question"].ToString();
        var answer = form["answer"].ToString();
        var model = form["model"].ToString();

        using var interview = new ResumeInterview(config);
        var r = await interview.EvaluateAsync(resume, stack, round, question, answer, model);
        return Results.Json(new { score = r.Score, feedback = r.Feedback, tips = r.Tips });
    });

    // Settings: paste one API key per provider. Keys are written to a local
    // per-user file and applied immediately (the shared config is reloaded).
    app.MapGet("/settings", () =>
        Results.Content(SettingsPage.Render(config, null), "text/html"));

    app.MapPost("/settings", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var keys = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var vendor in KeyStore.Vendors)
        {
            keys[vendor] = form[vendor].ToString();
        }

        KeyStore.Save(keys);

        // Reload the shared config so the new keys take effect right away. The
        // parameter is captured by every route, so reassigning it updates them.
        config = AppConfig.Load(ProjectPaths.ProjectRoot);

        var saved = keys.Count(k => !string.IsNullOrWhiteSpace(k.Value));
        var message = saved > 0
            ? $"Saved. {saved} key{(saved == 1 ? string.Empty : "s")} updated \u2014 the AI features are ready to use."
            : "Nothing changed \u2014 no new keys were entered.";
        return Results.Content(SettingsPage.Render(config, message), "text/html");
    });

    // Rapid drills: fast flashcards to make answers automatic.
    app.MapGet("/drills", (HttpRequest request) =>
    {
        var topic = request.Query["topic"].ToString();
        var shownTopic = string.IsNullOrWhiteSpace(topic) ? null : topic;
        return Results.Content(DrillsPage.Render(shownTopic), "text/html");
    });

    // Study plan: a focused multi-day plan linking into every mode.
    app.MapGet("/plan", () => Results.Content(StudyPlanPage.Render(), "text/html"));

    // Show a question for a topic (or a random one).
    app.MapGet("/practice", (HttpRequest request) =>
    {
        var topic = request.Query["topic"].ToString();
        var pool = string.IsNullOrWhiteSpace(topic)
            ? QuestionBank.Questions
            : QuestionBank.ForTopic(topic);

        Question? q = pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
        var shownTopic = string.IsNullOrWhiteSpace(topic) ? q?.Topic : topic;
        var html = PracticePage.Render(shownTopic, q, feedback: null, aiNote: null, config.HasOpenAi);
        return Results.Content(html, "text/html");
    });

    // Generate a FRESH question with the AI, tailored to the situation the user
    // typed (topic, role, job description, difficulty). Each request gives a new
    // question so "Next question" keeps producing different ones.
    app.MapPost("/practice/generate", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var situation = new PracticeSituation(
            Topic: form["gtopic"].ToString(),
            Role: form["grole"].ToString(),
            JobDescription: form["gjd"].ToString(),
            Difficulty: form["gdiff"].ToString());

        using var assistant = new StudyAssistant(config);
        var (questions, notice) = await assistant.GeneratePracticeQuestionsAsync(situation, 1);
        var q = questions.Count > 0 ? questions[0] : null;
        var shownTopic = q?.Topic
            ?? (string.IsNullOrWhiteSpace(situation.Topic) ? "Custom" : situation.Topic);

        var html = PracticePage.Render(
            shownTopic, q, feedback: null, aiNote: null, config.HasOpenAi, situation, notice);
        return Results.Content(html, "text/html");
    });

    // Score a submitted answer.
    app.MapPost("/answer", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var id = int.TryParse(form["id"], out var parsed) ? parsed : 0;
        var answer = form["answer"].ToString();

        var q = QuestionBank.ById(id);
        if (q is null)
        {
            return Results.Redirect("/practice");
        }

        var fb = scorer.Score(q, answer);

        string? aiNote = null;
        if (config.HasOpenAi)
        {
            using var coach = new OpenAiCoach(config);
            aiNote = await coach.CritiqueAsync(q, answer);
        }

        // Carry the AI "situation" (if this was a generated question) so the panel
        // stays filled and "Next question" can regenerate with the same context.
        var gtopic = form["gtopic"].ToString();
        var grole = form["grole"].ToString();
        var gjd = form["gjd"].ToString();
        var gdiff = form["gdiff"].ToString();
        PracticeSituation? situation =
            string.IsNullOrWhiteSpace(gtopic) && string.IsNullOrWhiteSpace(grole)
            && string.IsNullOrWhiteSpace(gjd) && string.IsNullOrWhiteSpace(gdiff)
                ? null
                : new PracticeSituation(gtopic, grole, gjd, gdiff);

        var html = PracticePage.Render(q.Topic, q, fb, aiNote, config.HasOpenAi, situation);
        return Results.Content(html, "text/html");
    });

    var port = Environment.GetEnvironmentVariable("PORT");
    var listenPort = string.IsNullOrWhiteSpace(port) ? "5095" : port;

    // Bind to all network interfaces (not just localhost) so a phone or tablet
    // on the SAME Wi-Fi can open the app and read answers there while you share
    // your PC screen (Option 3: phone as a private second screen).
    var url = $"http://0.0.0.0:{listenPort}";
    var lanIp = GetLanIp();
    var phoneUrl = lanIp is null ? null : $"http://{lanIp}:{listenPort}";
    NetworkInfo.Configure(phoneUrl);

    Console.WriteLine();
    Console.WriteLine("Interview Practice dashboard is running.");
    Console.WriteLine($"  On this PC:     http://localhost:{listenPort}");
    if (phoneUrl is not null)
    {
        Console.WriteLine($"  On your phone:  {phoneUrl}   (must be on the same Wi-Fi)");
        Console.WriteLine();
        Console.WriteLine("  Tip: open the phone address above on your phone (or scan the QR code");
        Console.WriteLine("       shown on the page), ask your questions and read the answers there.");
        Console.WriteLine("       Share your PC screen normally \u2014 nothing on the PC shows the answer.");
        Console.WriteLine("       If the phone can't load it, allow this app through Windows Firewall");
        Console.WriteLine("       on Private networks when prompted.");
    }

    Console.WriteLine();
    app.Run(url);
}

// Best-effort local IPv4 address for this machine on the LAN. Uses a UDP socket
// (no packets are actually sent) to discover which local address routes out.
static string? GetLanIp()
{
    try
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect("8.8.8.8", 65530);
        return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
    }
    catch
    {
        return null;
    }
}
