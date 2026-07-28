using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Services;

/// <summary>
/// A study assistant: answers a typed technical question so you can learn.
/// Uses OpenAI when configured; otherwise finds the closest question in the
/// built-in bank and returns its model answer (works fully offline).
/// </summary>
public sealed class StudyAssistant : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;

    public StudyAssistant(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<(string answer, string source)> AnswerAsync(
        string question, string? providerId = null, CancellationToken ct = default)
    {
        question = (question ?? string.Empty).Trim();
        if (question.Length == 0)
        {
            return ("Type a technical question to get an explained answer.", "info");
        }

        var provider = _config.GetProvider(providerId);

        // Try the requested provider first, then fall back to any other provider
        // that has a key. This way, if one model is down or out of quota, the user
        // still gets a real LLM answer instead of the offline bank text.
        var tryOrder = new List<AiProvider>();
        if (provider.HasKey)
        {
            tryOrder.Add(provider);
        }

        foreach (var p in _config.EnabledProviders)
        {
            if (!tryOrder.Any(x => x.Id == p.Id))
            {
                tryOrder.Add(p);
            }
        }

        foreach (var p in tryOrder)
        {
            var ai = await AskOpenAiAsync(question, p, ct);
            if (!string.IsNullOrWhiteSpace(ai))
            {
                return (ai!, p.DisplayName);
            }
        }

        return (BestLocalMatch(question), "study bank");
    }

    private async Task<string?> AskOpenAiAsync(string question, AiProvider provider, CancellationToken ct)
    {
        try
        {
            var system =
                "You are helping someone REHEARSE for a technical interview so they truly learn the " +
                "topic. Answer as an experienced Senior Software Engineer would explain it. Structure " +
                "your reply in THREE parts, EXACTLY in this order and format:\n" +
                "1) FIRST line must start with 'In short:' followed by a 1-2 sentence direct, simple " +
                "answer to the question \u2014 the quick version they can say immediately.\n" +
                "2) THEN a fuller explanation as 4 to 6 clear numbered points, each on its OWN line " +
                "beginning with '1.', '2.', '3.', '4.' and so on. Each point covers one key idea \u2014 " +
                "what it is, why it matters, how it works, or a trade-off / best practice \u2014 in 1-2 " +
                "sentences.\n" +
                "3) FINALLY one line that starts with 'Real example:' giving ONE concrete real-world " +
                "example that shows the concept in action (a scenario, a short code idea, or where it " +
                "is used in a real system).\n" +
                "Speak in a natural first-person tone (not a dry textbook) and use simple, clear " +
                "English because the person is not a native speaker. Do not add any other headings.";

            var payload = new
            {
                model = provider.Model,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = question },
                },
                temperature = 0.3,
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

            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the built-in question whose text overlaps most with the query and
    /// returns its model answer. Simple word-overlap scoring, no dependencies.
    /// </summary>
    private static string BestLocalMatch(string question)
    {
        var queryWords = Words(question);
        if (queryWords.Count == 0)
        {
            return "Try asking with a few more words about the concept you want to learn.";
        }

        Models.Question? best = null;
        var bestScore = 0;

        foreach (var q in QuestionBank.Questions)
        {
            var haystack = Words(q.Prompt + " " + string.Join(' ', q.KeyPoints));
            var score = queryWords.Count(w => haystack.Contains(w));
            if (score > bestScore)
            {
                bestScore = score;
                best = q;
            }
        }

        if (best is null || bestScore == 0)
        {
            return "I don't have a stored answer for that yet. Add an OpenAI key for open-ended " +
                   "answers, or try one of the practice topics: " + string.Join(", ", QuestionBank.Topics) + ".";
        }

        // Present the closest stored topic in the same three-part shape the LLM
        // uses: a short answer up top, then the fuller explanation.
        var sb = new StringBuilder();
        sb.Append("In short: ").Append(best.SimpleAnswer).Append('\n');
        sb.Append(best.ModelAnswer);
        if (best.KeyPoints.Count > 0)
        {
            sb.Append("\nReal example: think of ").Append(best.Prompt.TrimEnd('?', '.'))
              .Append(" \u2014 key things to mention are ")
              .Append(string.Join(", ", best.KeyPoints.Take(4))).Append('.');
        }

        return sb.ToString();
    }

    private static HashSet<string> Words(string text)
    {
        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what", "is", "the", "a", "an", "and", "or", "of", "to", "in", "on", "for",
            "how", "do", "does", "explain", "difference", "between", "why", "with", "are",
        };

        return new HashSet<string>(
            text.ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\n', '\r', '?', '.', ',', '(', ')', '\'', '"', '/', '-' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stop.Contains(w)),
            StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose() => _http.Dispose();
}
