using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;
using InterviewPrep.Models;

namespace InterviewPrep.Services;

/// <summary>The interviewer's verdict on one spoken answer: a clear
/// selected/rejected/borderline call, a score, an honest summary, the specific
/// drawbacks in the answer, where to improve, and a strong model answer.</summary>
public sealed record LiveVerdict(
    string Verdict,
    int Score,
    string Feedback,
    IReadOnlyList<string> Drawbacks,
    IReadOnlyList<string> Improve,
    string ModelAnswer);

/// <summary>
/// A realistic live interviewer for the webcam practice tab. You hear/read a
/// question, answer it out loud, and this returns whether you would be selected
/// or rejected, your weak points, and exactly what to improve. Uses AI when a
/// key is set; otherwise falls back to a simple offline judgement so it still
/// works without any provider.
/// </summary>
public sealed class LiveInterview : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private readonly Random _rng = new();

    public LiveInterview(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>Picks a question for a topic (or a random topic).</summary>
    public string NextQuestion(string? topic)
    {
        var pool = string.IsNullOrWhiteSpace(topic)
            ? QuestionBank.Questions
            : QuestionBank.ForTopic(topic);

        if (pool.Count == 0)
        {
            pool = QuestionBank.Questions;
        }

        return pool[_rng.Next(pool.Count)].Prompt;
    }

    /// <summary>Judges the spoken answer to the current question.</summary>
    public async Task<LiveVerdict> EvaluateAsync(
        string topic, string question, string spokenAnswer,
        string? providerId = null, CancellationToken ct = default)
    {
        question = (question ?? string.Empty).Trim();
        spokenAnswer = (spokenAnswer ?? string.Empty).Trim();

        var provider = _config.GetProvider(providerId);
        if (provider.HasKey)
        {
            var ai = await AskAiAsync(topic, question, spokenAnswer, provider, ct);
            if (ai is not null)
            {
                return ai;
            }
        }

        return LocalVerdict(question, spokenAnswer);
    }

    private async Task<LiveVerdict?> AskAiAsync(
        string topic, string question, string spokenAnswer, AiProvider provider, CancellationToken ct)
    {
        try
        {
            var system =
                "You are an experienced Senior Software Engineer running a real, fair technical " +
                "interview. The candidate is not a native English speaker and answered OUT LOUD, so " +
                "their answer is a speech-to-text transcript that may have small wording mistakes - " +
                "judge the ideas, not the grammar. Use simple, clear English. " +
                "You are given the question and the candidate's spoken answer. " +
                "Respond ONLY with a compact JSON object with exactly these fields: " +
                "\"verdict\" - exactly one of \"Selected\", \"Rejected\", or \"Borderline\"; " +
                "\"score\" - an integer 0 to 100 for this single answer; " +
                "\"feedback\" - 1-2 honest, encouraging sentences summarising the answer; " +
                "\"drawbacks\" - an array of 1-4 short strings, each a specific weak point or mistake " +
                "in what they said (empty array if the answer was excellent); " +
                "\"improve\" - an array of 1-4 short strings, each a concrete action to get better; " +
                "\"modelAnswer\" - how you would answer this question out loud in first person, natural " +
                "spoken tone, 3-6 sentences, ending with a short line that starts with 'Say it simply:'. " +
                "Do not add any text outside the JSON.";

            var user =
                $"Topic: {(string.IsNullOrWhiteSpace(topic) ? "General" : topic)}\n" +
                $"Question: {question}\n" +
                $"Candidate's spoken answer: {(spokenAnswer.Length == 0 ? "(said nothing)" : spokenAnswer)}";

            var payload = new
            {
                model = provider.Model,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                temperature = 0.3,
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
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var inner = JsonDocument.Parse(content);
            var root = inner.RootElement;

            var verdict = NormalizeVerdict(GetString(root, "verdict"));
            var score = GetInt(root, "score");
            var feedback = GetString(root, "feedback");
            var drawbacks = GetArray(root, "drawbacks");
            var improve = GetArray(root, "improve");
            var modelAnswer = GetString(root, "modelAnswer");

            return new LiveVerdict(verdict, score, feedback, drawbacks, improve, modelAnswer);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeVerdict(string raw)
    {
        var v = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (v.StartsWith("select") || v.Contains("pass") || v.Contains("hire")) return "Selected";
        if (v.StartsWith("reject") || v.Contains("fail") || v.Contains("no hire")) return "Rejected";
        return "Borderline";
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

        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s))
        {
            return Math.Clamp(s, 0, 100);
        }

        return 0;
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

    /// <summary>Offline judgement when no AI key is set: a simple length/keyword
    /// heuristic plus the question bank's model answer, so the tab still works.</summary>
    private LiveVerdict LocalVerdict(string question, string spokenAnswer)
    {
        var asked = QuestionBank.Questions
            .FirstOrDefault(q => string.Equals(q.Prompt, question, StringComparison.OrdinalIgnoreCase));

        var model = asked?.ModelAnswer ?? "Give a clear, structured answer and back it with an example.";
        if (asked is not null && !string.IsNullOrWhiteSpace(asked.SimpleAnswer))
        {
            model += "\n\nSay it simply: " + asked.SimpleAnswer;
        }

        var words = spokenAnswer.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        string verdict;
        int score;
        var drawbacks = new List<string>();
        var improve = new List<string>();
        string feedback;

        if (words == 0)
        {
            verdict = "Rejected";
            score = 0;
            feedback = "No answer was heard, so this would be a reject in a real interview.";
            drawbacks.Add("You did not give any answer the interviewer could hear.");
            improve.Add("Start speaking within a few seconds; say your main point first.");
            improve.Add("If you are unsure, say what you DO know instead of staying silent.");
        }
        else if (words < 15)
        {
            verdict = "Borderline";
            score = 45;
            feedback = "Too short to judge fully - the idea is there but it needs more.";
            drawbacks.Add("The answer was very short and missing detail.");
            drawbacks.Add("No example was given to prove your point.");
            improve.Add("Aim for 4-6 sentences with one concrete example.");
            improve.Add("Lead with the key point, then explain why.");
        }
        else
        {
            verdict = "Selected";
            score = 72;
            feedback = "A solid, real explanation - tighten it and lead with the key point.";
            drawbacks.Add("Could be more structured (point, reason, example).");
            improve.Add("Start with a one-line summary before the detail.");
            improve.Add("Finish with a short 'in short...' line to sound confident.");
        }

        feedback += " (Offline check - add an AI key for a real interviewer judgement.)";
        return new LiveVerdict(verdict, score, feedback, drawbacks, improve, model);
    }

    public void Dispose() => _http.Dispose();
}
