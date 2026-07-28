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

    // Last-seen "remaining quota" per provider Id, so the model picker can show
    // a compact number (e.g. "98,540 tok left") next to each model BEFORE you
    // ask again. Updated every time a provider replies with rate-limit headers.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
        LastUsageById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Compact remaining-quota text last seen for a provider, or null
    /// if that provider has not reported rate-limit headers yet this session.</summary>
    public static string? GetLastUsage(string providerId) =>
        !string.IsNullOrWhiteSpace(providerId)
        && LastUsageById.TryGetValue(providerId, out var v)
            ? v
            : null;

    public StudyAssistant(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<(string answer, string source, string? notice, string? usage)> AnswerAsync(
        string question, string? providerId = null, CancellationToken ct = default)
    {
        question = (question ?? string.Empty).Trim();
        if (question.Length == 0)
        {
            return ("Type a technical question to get an explained answer.", "info", null, null);
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

        // Remember the most informative failure so we can tell the user WHY the
        // answer came from the offline study bank instead of the live model.
        var worstFailure = AiFailure.None;
        AiProvider? failedProvider = null;
        string? lastUsage = null;

        foreach (var p in tryOrder)
        {
            var (ai, failure, usage) = await AskOpenAiAsync(question, p, ct);
            if (!string.IsNullOrWhiteSpace(ai))
            {
                return (ai!, p.DisplayName, null, usage);
            }

            // Quota is the most important reason to surface; then invalid key.
            if (failure > worstFailure)
            {
                worstFailure = failure;
                failedProvider = p;
                lastUsage = usage;
            }
        }

        var notice = BuildNotice(worstFailure, failedProvider, tryOrder.Count);
        return (BestLocalMatch(question), "study bank", notice, lastUsage);
    }

    /// <summary>Why a live AI call could not be used. Higher value = more
    /// important to tell the user about.</summary>
    private enum AiFailure
    {
        None = 0,
        Other = 1,
        InvalidKey = 2,
        Quota = 3,
    }

    private static string? BuildNotice(AiFailure failure, AiProvider? provider, int triedCount)
    {
        var name = provider?.DisplayName ?? "The AI model";
        return failure switch
        {
            AiFailure.Quota =>
                $"\u26a0\ufe0f {name} has hit its rate limit or daily quota, so this answer came from the " +
                "offline study bank. Wait a bit and try again, or switch to another model \u2014 the free " +
                "Groq tier resets every day (about 100,000 tokens / 1,000 requests per day).",
            AiFailure.InvalidKey =>
                $"\u26a0\ufe0f {name}\u2019s API key is missing or invalid, so this answer came from the offline " +
                "study bank. Add a valid key in appsettings.Local.json to get live AI answers.",
            AiFailure.Other when triedCount > 0 =>
                "\u26a0\ufe0f The AI model could not be reached, so this answer came from the offline study " +
                "bank. Check your internet connection and try again.",
            _ => null,
        };
    }

    private async Task<(string? content, AiFailure failure, string? usage)> AskOpenAiAsync(
        string question, AiProvider provider, CancellationToken ct)
    {
        if (!provider.HasKey)
        {
            return (null, AiFailure.InvalidKey, null);
        }

        try
        {
            var system =
                "You are helping someone REHEARSE for a technical interview so they truly learn the " +
                "topic. Answer as an experienced Senior Software Engineer would explain it.\n" +
                "ACCURACY IS THE TOP PRIORITY. Follow these rules strictly:\n" +
                "- Be technically precise and factually correct; use current, widely-accepted best " +
                "practices and correct terminology.\n" +
                "- Do NOT invent facts, APIs, numbers, or behavior. If something depends on a version, " +
                "language, or context, say so briefly.\n" +
                "- If you are not sure or the question is ambiguous, state the assumption you are making " +
                "in one short clause instead of guessing.\n" +
                "- Prefer concrete, verifiable details over vague generalities, but keep it concise.\n" +
                "Structure your reply in THREE parts, EXACTLY in this order and format:\n" +
                "1) FIRST line must start with 'In short:' followed by a 1-2 sentence direct, simple " +
                "answer to the question \u2014 the quick version they can say immediately.\n" +
                "2) THEN a fuller explanation as 4 to 6 clear numbered points, each on its OWN line " +
                "beginning with '1.', '2.', '3.', '4.' and so on. Each point covers one key idea \u2014 " +
                "what it is, why it matters, how it works, or a trade-off / best practice \u2014 in 1-2 " +
                "sentences.\n" +
                "3) FINALLY one line that starts with 'Real example:' giving ONE concrete, accurate " +
                "real-world example that shows the concept in action (a scenario, a short code idea, or " +
                "where it is used in a real system).\n" +
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
                temperature = 0.15,
                top_p = 0.9,
                max_tokens = 900,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, provider.BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            var usage = BuildUsage(response, provider);
            if (!response.IsSuccessStatusCode)
            {
                var failure = (int)response.StatusCode switch
                {
                    429 => AiFailure.Quota,
                    401 or 403 => AiFailure.InvalidKey,
                    _ => AiFailure.Other,
                };
                return (null, failure, usage);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? (null, AiFailure.Other, usage)
                : (content!.Trim(), AiFailure.None, usage);
        }
        catch
        {
            return (null, AiFailure.Other, null);
        }
    }

    /// <summary>Reads the provider's rate-limit response headers (Groq/OpenAI
    /// style) and builds a short "tokens/requests left today" string, or null
    /// when the provider does not report them.</summary>
    private static string? BuildUsage(HttpResponseMessage response, AiProvider provider)
    {
        var remTokens = Header(response, "x-ratelimit-remaining-tokens");
        var remRequests = Header(response, "x-ratelimit-remaining-requests");
        if (remTokens is null && remRequests is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (remTokens is not null)
        {
            parts.Add($"{FormatCount(remTokens)} tokens left");
        }

        if (remRequests is not null)
        {
            parts.Add($"{FormatCount(remRequests)} requests left");
        }

        var reset = Header(response, "x-ratelimit-reset-tokens")
                    ?? Header(response, "x-ratelimit-reset-requests");
        var text = $"{provider.DisplayName}: " + string.Join(" \u00b7 ", parts);
        if (!string.IsNullOrWhiteSpace(reset))
        {
            text += $" (resets in {reset})";
        }

        // Cache a compact form (no provider name) for the model picker dropdown.
        var compact = string.Join(" \u00b7 ", parts)
            .Replace(" tokens left", " tok")
            .Replace(" requests left", " req");
        LastUsageById[provider.Id] = compact;

        return text;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;

    private static string FormatCount(string raw) =>
        long.TryParse(raw, out var n) ? n.ToString("N0") : raw;

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
