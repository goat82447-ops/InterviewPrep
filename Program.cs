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

// A real command-line coding agent, like Copilot CLI: name a project, pick where
// to put it, describe it, and it scaffolds the whole project on disk. Loops until
// you type 'quit'. Never writes into this app's own project folder.
static async Task RunAgentCliAsync(AppConfig config)
{
    using var agent = new CodeAgent(config);

    Console.WriteLine("=== Agent CLI \u2014 build a new project ===");
    Console.WriteLine(config.HasAnyAi
        ? "AI: on. Describe a project and it will be created on disk."
        : "AI: OFF \u2014 add a free API key in appsettings.Local.json first.");
    Console.WriteLine($"Default location: {agent.DefaultBase}");
    Console.WriteLine("Type 'quit' at any prompt to exit.");
    Console.WriteLine();

    while (true)
    {
        Console.Write("Project name: ");
        var name = Console.ReadLine()?.Trim();
        if (IsQuit(name))
        {
            break;
        }

        Console.Write($"Location (Enter for Desktop, or a path like C:\\Projects): ");
        var location = Console.ReadLine()?.Trim();
        if (IsQuit(location))
        {
            break;
        }

        Console.Write("Describe the project: ");
        var task = Console.ReadLine()?.Trim();
        if (IsQuit(task))
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(task))
        {
            Console.WriteLine("Please describe what to build.\n");
            continue;
        }

        Console.WriteLine("Working\u2026");
        var (message, files, notice, source, projectFolder) =
            await agent.RunAsync(task!, name, location);

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
            }

            Console.WriteLine("Done. Open the folder above to build and run it.");
        }
        else if (string.IsNullOrWhiteSpace(notice))
        {
            Console.WriteLine("No files were created.");
        }

        Console.WriteLine();
    }

    Console.WriteLine("Bye.");

    static bool IsQuit(string? s) =>
        string.Equals(s, "quit", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s, "exit", StringComparison.OrdinalIgnoreCase);
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
    app.MapGet("/ask", () =>
        Results.Content(
            AskPage.Render(null, null, null, config.HasOpenAi,
                config.Providers, config.GetProvider(null).Id),
            "text/html"));

    app.MapPost("/ask", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var question = form["question"].ToString();
        var model = form["model"].ToString();

        using var assistant = new StudyAssistant(config);
        var (answer, source, notice, usage) = await assistant.AnswerAsync(question, model);

        var selected = config.GetProvider(model).Id;
        var html = AskPage.Render(question, answer, source, config.HasOpenAi,
            config.Providers, selected, notice, usage);
        return Results.Content(html, "text/html");
    });

    // JSON answer endpoint — lets the private floating window ask a new question
    // and show the answer inside itself, without switching back to the shared tab.
    app.MapPost("/ask-json", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var question = form["question"].ToString();
        var model = form["model"].ToString();

        using var assistant = new StudyAssistant(config);
        var (answer, source, notice, usage) = await assistant.AnswerAsync(question, model);
        return Results.Json(new { answer, source, notice, usage, html = AnswerFormat.ToHtml(answer) });
    });

    // Start a fresh topic — forget the running chat so the next question has no
    // earlier context. Used by the "New topic" button on the Ask page.
    app.MapPost("/ask/reset", () =>
    {
        StudyAssistant.ClearConversation();
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
