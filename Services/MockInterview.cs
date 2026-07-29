using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;
using InterviewPrep.Models;

namespace InterviewPrep.Services;

/// <summary>One turn of a mock interview: coaching on the answer just given, a
/// strong spoken model answer, and a natural follow-up question to keep going.</summary>
public sealed record MockTurn(string Feedback, string ModelAnswer, string FollowUp);

/// <summary>
/// Runs a realistic mock interview. It reacts to YOUR answer the way a real
/// interviewer would: a short honest critique, a strong model answer to learn
/// from, and a follow-up question that digs deeper. Uses AI when configured;
/// otherwise falls back to the built-in question bank so it still works offline.
/// </summary>
public sealed class MockInterview : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private readonly Random _rng = new();

    /// <summary>One question the interviewer asked and how the candidate replied.</summary>
    private readonly record struct Exchange(string Question, string Answer);

    // The running interview transcript, shared across requests so the AI can ask
    // follow-ups that build on everything said so far (a real interview remembers).
    private static readonly object ConvLock = new();
    private static readonly List<Exchange> History = new();
    private const int MaxExchanges = 8;

    public MockInterview(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>Clears the transcript so the next question starts a fresh interview.</summary>
    public static void ResetInterview()
    {
        lock (ConvLock)
        {
            History.Clear();
        }
    }

    private static IReadOnlyList<Exchange> Snapshot()
    {
        lock (ConvLock)
        {
            return History.ToList();
        }
    }

    private static void Record(string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return;
        }

        lock (ConvLock)
        {
            History.Add(new Exchange(question, answer));
            if (History.Count > MaxExchanges)
            {
                History.RemoveRange(0, History.Count - MaxExchanges);
            }
        }
    }

    /// <summary>Picks an opening question for a topic (or a random topic).</summary>
    public string FirstQuestion(string? topic)
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

    /// <summary>
    /// Given the question the candidate just answered and their answer, returns
    /// coaching, a strong model answer, and the next follow-up question.
    /// </summary>
    public async Task<MockTurn> NextAsync(
        string topic, string question, string userAnswer,
        string? providerId = null, CancellationToken ct = default)
    {
        question = (question ?? string.Empty).Trim();
        userAnswer = (userAnswer ?? string.Empty).Trim();

        var history = Snapshot();

        var provider = _config.GetProvider(providerId);
        if (provider.HasKey)
        {
            var ai = await AskAiAsync(topic, question, userAnswer, history, provider, ct);
            if (ai is not null)
            {
                Record(question, userAnswer);
                return ai;
            }
        }

        Record(question, userAnswer);
        return LocalTurn(topic, question, userAnswer);
    }

    private async Task<MockTurn?> AskAiAsync(
        string topic, string question, string userAnswer,
        IReadOnlyList<Exchange> history, AiProvider provider, CancellationToken ct)
    {
        try
        {
            var system =
                "You are an experienced Senior Software Engineer conducting a friendly but real " +
                "technical interview. The candidate is not a native English speaker, so use simple, " +
                "clear English. You will be given the question you asked and the candidate's answer. " +
                "Respond ONLY with a compact JSON object with exactly these string fields: " +
                "\"feedback\" - 1-2 honest, encouraging sentences on what was good and what was missing; " +
                "\"modelAnswer\" - how you would answer that same question out loud in an interview, " +
                "first person, natural spoken tone, 3-6 sentences, ending with a short line that starts " +
                "with 'Say it simply:'; " +
                "\"followUp\" - one natural follow-up question that builds on what the candidate has " +
                "already said in this interview, digs a little deeper, and does NOT repeat an earlier " +
                "question, exactly as a real interviewer would ask next. " +
                "Do not add any text outside the JSON.";

            var transcript = new StringBuilder();
            if (history.Count > 0)
            {
                transcript.Append("Interview so far (oldest first):\n");
                var n = 1;
                foreach (var ex in history)
                {
                    transcript.Append($"Q{n}: {ex.Question}\n");
                    transcript.Append($"A{n}: {(string.IsNullOrWhiteSpace(ex.Answer) ? "(no answer)" : ex.Answer)}\n");
                    n++;
                }

                transcript.Append('\n');
            }

            var user =
                transcript.ToString() +
                $"Topic: {topic}\n" +
                $"Current question you asked: {question}\n" +
                $"Candidate's answer: {(userAnswer.Length == 0 ? "(no answer given)" : userAnswer)}";

            var payload = new
            {
                model = provider.Model,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                temperature = 0.4,
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
            var feedback = GetString(root, "feedback");
            var modelAnswer = GetString(root, "modelAnswer");
            var followUp = GetString(root, "followUp");

            if (string.IsNullOrWhiteSpace(followUp))
            {
                followUp = FirstQuestion(topic);
            }

            return new MockTurn(feedback, modelAnswer, followUp);
        }
        catch
        {
            return null;
        }
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()!.Trim()
            : string.Empty;

    /// <summary>Offline fallback using the question bank.</summary>
    private MockTurn LocalTurn(string topic, string question, string userAnswer)
    {
        var asked = QuestionBank.Questions
            .FirstOrDefault(q => string.Equals(q.Prompt, question, StringComparison.OrdinalIgnoreCase));

        var model = asked?.ModelAnswer ?? "Give a clear, structured answer and back it with an example.";
        if (asked is not null && !string.IsNullOrWhiteSpace(asked.SimpleAnswer))
        {
            model += "\n\nSay it simply: " + asked.SimpleAnswer;
        }

        var words = userAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var feedback = userAnswer.Length == 0
            ? "No answer yet - take a breath and try it in your own words; there is a model answer below to learn from."
            : words < 12
                ? "Good start. Add a little more detail and a short example to make it convincing."
                : "Nice - you gave a real explanation. Tighten it and lead with the key point.";

        return new MockTurn(feedback, model, FirstQuestion(topic));
    }

    public void Dispose() => _http.Dispose();
}
