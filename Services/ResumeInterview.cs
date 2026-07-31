using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Services;

/// <summary>One round of the resume-based interview: an id, a friendly name, how
/// many questions it asks, what it focuses on, and an emoji for the UI.</summary>
public sealed record RoundInfo(string Id, string Name, int Count, string Focus, string Emoji);

/// <summary>
/// A full resume-driven mock interview: two technical rounds, one managerial
/// round and one HR round. Every question is generated from the candidate's own
/// resume and tech stack, and every answer is scored with concrete tips. Uses AI
/// when a key is set; otherwise falls back to a built-in question bank and a
/// simple offline judgement so the page still works without any provider.
/// </summary>
public sealed class ResumeInterview : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private readonly Random _rng = new();

    public ResumeInterview(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>The fixed round plan: 2 technical, 1 managerial, 1 HR.</summary>
    public static readonly IReadOnlyList<RoundInfo> Rounds = new[]
    {
        new RoundInfo("tech1", "Technical Round 1", 3,
            "core technical fundamentals of the candidate's main tech stack", "\ud83e\udde0"),
        new RoundInfo("tech2", "Technical Round 2", 3,
            "deeper scenario-based and system-design questions tied to the real projects and experience in the resume", "\ud83c\udfd7\ufe0f"),
        new RoundInfo("managerial", "Managerial Round", 2,
            "managerial and behavioural questions about ownership, teamwork, conflict, deadlines and decisions, tied to their real experience", "\ud83d\udc54"),
        new RoundInfo("hr", "HR Round", 2,
            "HR questions about motivation, strengths, weaknesses, career goals, salary expectations and company fit", "\ud83e\udd1d"),
    };

    public static RoundInfo? GetRound(string? id) =>
        Rounds.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Generates the next question for a round, avoiding ones already asked.</summary>
    public async Task<string> NextQuestionAsync(
        string resume, string stack, string roundId, IEnumerable<string> asked,
        string? providerId = null, CancellationToken ct = default)
    {
        var round = GetRound(roundId) ?? Rounds[0];
        var askedList = (asked ?? Array.Empty<string>())
            .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();

        var provider = _config.GetProvider(providerId);
        if (provider.HasKey)
        {
            var q = await AskAiQuestionAsync(resume, stack, round, askedList, provider, ct);
            if (!string.IsNullOrWhiteSpace(q))
            {
                return q!.Trim();
            }
        }

        return OfflineQuestion(round.Id, askedList);
    }

    /// <summary>Scores one answer for a round and returns short, concrete tips.</summary>
    public async Task<(int Score, string Feedback, IReadOnlyList<string> Tips)> EvaluateAsync(
        string resume, string stack, string roundId, string question, string answer,
        string? providerId = null, CancellationToken ct = default)
    {
        question = (question ?? string.Empty).Trim();
        answer = (answer ?? string.Empty).Trim();
        var round = GetRound(roundId) ?? Rounds[0];

        var provider = _config.GetProvider(providerId);
        if (provider.HasKey)
        {
            var ai = await AskAiEvaluateAsync(resume, stack, round, question, answer, provider, ct);
            if (ai is not null)
            {
                return ai.Value;
            }
        }

        return OfflineEvaluate(answer);
    }

    private async Task<string?> AskAiQuestionAsync(
        string resume, string stack, RoundInfo round, IReadOnlyList<string> asked,
        AiProvider provider, CancellationToken ct)
    {
        try
        {
            var system =
                $"You are a senior interviewer conducting the '{round.Name}' for a candidate. " +
                $"Based ONLY on the candidate's resume and tech stack, ask ONE realistic interview " +
                $"question focused on {round.Focus}. Make it specific to what is actually in their " +
                "resume where possible. Keep it to one or two sentences. Do NOT repeat any question " +
                "in the already-asked list. Respond ONLY with a compact JSON object with a single " +
                "field \"question\" containing the question text. No text outside the JSON.";

            var askedText = asked.Count == 0 ? "(none yet)" : string.Join("\n", asked.Select(a => "- " + a));
            var user =
                $"Tech stack: {(string.IsNullOrWhiteSpace(stack) ? "(not given)" : stack)}\n\n" +
                $"Resume:\n{Truncate(resume, 6000)}\n\n" +
                $"Already asked in this round:\n{askedText}";

            var content = await ChatJsonAsync(provider, system, user, 0.6, ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var inner = JsonDocument.Parse(content);
            return GetString(inner.RootElement, "question");
        }
        catch
        {
            return null;
        }
    }

    private async Task<(int, string, IReadOnlyList<string>)?> AskAiEvaluateAsync(
        string resume, string stack, RoundInfo round, string question, string answer,
        AiProvider provider, CancellationToken ct)
    {
        try
        {
            var lens = round.Id is "managerial" or "hr"
                ? "Judge communication, structure (Situation-Task-Action-Result), honesty and confidence, not code."
                : "Judge technical correctness, depth, and whether they gave a concrete example.";

            var system =
                $"You are a fair, experienced interviewer in the '{round.Name}'. {lens} " +
                "The candidate is not a native English speaker and may have answered out loud, so the " +
                "text can be a rough transcript - judge the ideas, not the grammar. Use simple, clear " +
                "English. Respond ONLY with a compact JSON object with exactly these fields: " +
                "\"score\" - an integer 0 to 100 for this single answer; " +
                "\"feedback\" - 1-2 honest but encouraging sentences; " +
                "\"tips\" - an array of 1-3 short strings, each a concrete way to improve this answer. " +
                "No text outside the JSON.";

            var user =
                $"Tech stack: {(string.IsNullOrWhiteSpace(stack) ? "(not given)" : stack)}\n" +
                $"Question: {question}\n" +
                $"Candidate's answer: {(answer.Length == 0 ? "(said nothing)" : answer)}";

            var content = await ChatJsonAsync(provider, system, user, 0.3, ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var inner = JsonDocument.Parse(content);
            var root = inner.RootElement;
            return (GetInt(root, "score"), GetString(root, "feedback"), GetArray(root, "tips"));
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> ChatJsonAsync(
        AiProvider provider, string system, string user, double temperature, CancellationToken ct)
    {
        var payload = new
        {
            model = provider.Model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
            temperature,
            response_format = new { type = "json_object" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, provider.BaseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private string OfflineQuestion(string roundId, IReadOnlyList<string> asked)
    {
        var pool = OfflineBank.TryGetValue(roundId, out var list) ? list : OfflineBank["tech1"];
        var remaining = pool.Where(q => !asked.Contains(q, StringComparer.OrdinalIgnoreCase)).ToList();
        if (remaining.Count == 0)
        {
            remaining = pool;
        }

        return remaining[_rng.Next(remaining.Count)];
    }

    private static (int, string, IReadOnlyList<string>) OfflineEvaluate(string answer)
    {
        var words = answer.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words == 0)
        {
            return (0, "No answer was given, so this would be a reject in a real interview. (Offline check - add an AI key for a real judgement.)",
                new[] { "Start with your main point within a few seconds.", "If unsure, say what you DO know instead of staying silent." });
        }

        if (words < 15)
        {
            return (45, "Too short to judge fully - the idea is there but it needs more. (Offline check - add an AI key for a real judgement.)",
                new[] { "Aim for 4-6 sentences with one concrete example.", "Lead with the key point, then explain why." });
        }

        return (72, "A solid, real answer - tighten it and lead with the key point. (Offline check - add an AI key for a real judgement.)",
            new[] { "Start with a one-line summary before the detail.", "Finish with a short 'in short...' line to sound confident." });
    }

    private static readonly IReadOnlyDictionary<string, List<string>> OfflineBank =
        new Dictionary<string, List<string>>
        {
            ["tech1"] = new()
            {
                "Explain the difference between value types and reference types, with an example.",
                "What is dependency injection and why is it useful?",
                "How does async/await work, and when would you avoid it?",
                "What is the difference between an abstract class and an interface?",
                "How do you handle exceptions and logging in a production service?",
                "Explain the main data structures you use and when you pick each one.",
            },
            ["tech2"] = new()
            {
                "Walk me through how you would design a CI/CD pipeline for a service you have worked on.",
                "Describe a production incident you handled and how you found the root cause.",
                "How would you scale a web API that is getting slow under heavy load?",
                "How do you make a deployment safe and easy to roll back?",
                "Explain how you would design monitoring and alerting for a critical system.",
                "Tell me about a design decision you made and the trade-offs involved.",
            },
            ["managerial"] = new()
            {
                "Tell me about a time you disagreed with a teammate and how you resolved it.",
                "How do you prioritise when everything feels urgent?",
                "Describe a project you owned end to end and the outcome.",
                "How do you handle a tight deadline you know you cannot fully meet?",
            },
            ["hr"] = new()
            {
                "Why are you looking to change your job?",
                "What are your biggest strengths and one real weakness?",
                "Where do you see yourself in five years?",
                "What are your salary expectations, and why?",
            },
        };

    private static string Truncate(string? s, int max)
    {
        s ??= string.Empty;
        return s.Length <= max ? s : s.Substring(0, max) + " …";
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()!.Trim()
            : string.Empty;

    private static int GetInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return 0;
        }

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            return Math.Clamp(n, 0, 100);
        }

        return el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)
            ? Math.Clamp(s, 0, 100)
            : 0;
    }

    private static IReadOnlyList<string> GetArray(JsonElement root, string name)
    {
        var list = new List<string>();
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        list.Add(s.Trim());
                    }
                }
            }
        }

        return list;
    }

    public void Dispose() => _http.Dispose();
}
