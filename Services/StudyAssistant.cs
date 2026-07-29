using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;
using InterviewPrep.Models;

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

    // ----- Conversation memory (ChatGPT-style follow-ups) --------------------
    // This is a personal, single-user app, so the running chat is kept here in
    // memory. Each successful AI answer is remembered so follow-up questions
    // ("explain simpler", "give an example") keep the earlier context. Only the
    // last few messages are kept to stay well inside the free token budget.

    /// <summary>One line of the running conversation.</summary>
    public readonly record struct ChatTurn(string Role, string Content);

    private static readonly object ConvLock = new();

    // Chat is saved to this small file so follow-up context survives an app
    // restart. It lives at the project root and is git-ignored (personal data).
    private static readonly string ConvFile =
        Path.Combine(ProjectPaths.ProjectRoot, "conversation.json");

    private static readonly List<ChatTurn> Conversation = LoadConversation();
    private const int MaxHistoryMessages = 6; // ~3 back-and-forth exchanges

    /// <summary>The current chat transcript (oldest first).</summary>
    public static IReadOnlyList<ChatTurn> GetConversation()
    {
        lock (ConvLock)
        {
            return Conversation.ToArray();
        }
    }

    /// <summary>Starts a fresh topic by forgetting the earlier conversation.</summary>
    public static void ClearConversation()
    {
        lock (ConvLock)
        {
            Conversation.Clear();
            SaveConversation();
        }
    }

    private static ChatTurn[] SnapshotHistory()
    {
        lock (ConvLock)
        {
            return Conversation.ToArray();
        }
    }

    private static void RecordTurn(string question, string answer)
    {
        lock (ConvLock)
        {
            Conversation.Add(new ChatTurn("user", question));
            Conversation.Add(new ChatTurn("assistant", answer));
            while (Conversation.Count > MaxHistoryMessages)
            {
                Conversation.RemoveAt(0);
            }

            SaveConversation();
        }
    }

    /// <summary>Loads the saved chat from disk (best-effort). Returns an empty
    /// list if the file is missing or unreadable, so the app always starts.</summary>
    private static List<ChatTurn> LoadConversation()
    {
        try
        {
            if (File.Exists(ConvFile))
            {
                var saved = JsonSerializer.Deserialize<List<ChatTurn>>(
                    File.ReadAllText(ConvFile));
                if (saved is not null)
                {
                    if (saved.Count > MaxHistoryMessages)
                    {
                        saved.RemoveRange(0, saved.Count - MaxHistoryMessages);
                    }

                    return saved;
                }
            }
        }
        catch
        {
            // Missing or corrupt file \u2014 just start with an empty chat.
        }

        return new List<ChatTurn>();
    }

    /// <summary>Writes the current chat to disk (best-effort). Callers hold
    /// <see cref="ConvLock"/>. Disk errors are ignored so answering never fails.</summary>
    private static void SaveConversation()
    {
        try
        {
            File.WriteAllText(ConvFile, JsonSerializer.Serialize(Conversation));
        }
        catch
        {
            // Persisting is best-effort; ignore disk errors.
        }
    }

    public StudyAssistant(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
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

        // Snapshot the running chat so follow-up questions keep their context.
        var history = SnapshotHistory();

        foreach (var p in tryOrder)
        {
            var (ai, failure, usage) = await AskOpenAiAsync(question, p, history, ct);
            if (!string.IsNullOrWhiteSpace(ai))
            {
                RecordTurn(question, ai!);
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

    /// <summary>
    /// Generates fresh, tailored interview questions with model answers using the
    /// AI, based on the situation the user describes (topic, role, job description,
    /// difficulty). Returns an empty list plus a notice when no AI key is set or
    /// the model could not be reached.
    /// </summary>
    public async Task<(IReadOnlyList<Question> questions, string? notice)> GeneratePracticeQuestionsAsync(
        PracticeSituation situation, int count = 1, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 6);

        var providers = _config.EnabledProviders.ToList();
        if (providers.Count == 0)
        {
            return (Array.Empty<Question>(),
                "\u26a0\ufe0f No AI key is set, so questions can\u2019t be generated. Add a free key in " +
                "appsettings.Local.json (see FREE_AI_KEYS.md) and restart.");
        }

        var topic = FirstNonEmpty(situation.Topic, situation.Role, "General");

        var system =
            "You are an expert technical interviewer. Generate realistic interview questions tailored to " +
            "the candidate's situation. Return ONLY a JSON array \u2014 no prose, no markdown code fences. " +
            "Each array element is an object with EXACTLY these fields: " +
            "\"prompt\" (the interview question), " +
            "\"level\" (one of \"Easy\", \"Medium\", \"Hard\"), " +
            "\"modelAnswer\" (a strong, accurate, senior-level answer, 3-6 sentences), " +
            "\"simpleAnswer\" (a short easy-English version to say aloud, 1-2 sentences), " +
            "\"keyPoints\" (an array of 4 to 7 short lowercase keywords or phrases the ideal answer should " +
            "mention \u2014 these are used to score the candidate). Be technically accurate; do not invent APIs.";

        var user = BuildSituationPrompt(situation, count);

        var worst = AiFailure.None;
        AiProvider? failed = null;
        foreach (var p in providers)
        {
            var (content, failure, _) = await PostChatAsync(p, system, user, 0.7, ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                var qs = ParseAndRegister(content!, topic);
                if (qs.Count > 0)
                {
                    return (qs, null);
                }
            }

            if (failure > worst)
            {
                worst = failure;
                failed = p;
            }
        }

        return (Array.Empty<Question>(), BuildNotice(worst, failed, providers.Count));
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "General";

    private static string BuildSituationPrompt(PracticeSituation s, int count)
    {
        var sb = new StringBuilder();
        sb.Append($"Generate {count} interview question(s) for this candidate.\n");
        if (!string.IsNullOrWhiteSpace(s.Topic))
        {
            sb.Append($"Topic / skill: {s.Topic}\n");
        }

        if (!string.IsNullOrWhiteSpace(s.Role))
        {
            sb.Append($"Role / seniority: {s.Role}\n");
        }

        if (!string.IsNullOrWhiteSpace(s.Difficulty)
            && !s.Difficulty.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($"Difficulty: {s.Difficulty}\n");
        }

        if (!string.IsNullOrWhiteSpace(s.JobDescription))
        {
            sb.Append($"Job description:\n{s.JobDescription}\n");
        }

        sb.Append("Make the questions realistic and specific to the details above. Vary them so that " +
                  "repeated requests produce different questions.");
        return sb.ToString();
    }

    private static IReadOnlyList<Question> ParseAndRegister(string content, string topic)
    {
        var list = new List<Question>();
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return list;
        }

        try
        {
            using var doc = JsonDocument.Parse(content.Substring(start, end - start + 1));
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var prompt = GetStr(el, "prompt");
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    continue;
                }

                var levelStr = GetStr(el, "level");
                var level = levelStr.Equals("Hard", StringComparison.OrdinalIgnoreCase) ? Level.Hard
                    : levelStr.Equals("Easy", StringComparison.OrdinalIgnoreCase) ? Level.Easy
                    : Level.Medium;

                var model = GetStr(el, "modelAnswer");
                var simple = GetStr(el, "simpleAnswer");

                var points = new List<string>();
                if (el.TryGetProperty("keyPoints", out var kp) && kp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var k in kp.EnumerateArray())
                    {
                        var s = k.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            points.Add(s!.Trim());
                        }
                    }
                }

                list.Add(QuestionBank.RegisterGenerated(topic, level, prompt!, model, simple, points));
            }
        }
        catch (JsonException)
        {
            // Malformed JSON from the model — return whatever parsed so far.
        }

        return list;
    }

    private static string GetStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!.Trim()
            : string.Empty;

    /// <summary>Low-level single-turn chat call used for tasks other than the
    /// study answer (e.g. generating practice questions).</summary>
    private async Task<(string? content, AiFailure failure, string? usage)> PostChatAsync(
        AiProvider provider, string system, string user, double temperature, CancellationToken ct)
    {
        if (!provider.HasKey)
        {
            return (null, AiFailure.InvalidKey, null);
        }

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
                temperature,
                max_tokens = 1600,
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
                : (content, AiFailure.None, usage);
        }
        catch
        {
            return (null, AiFailure.Other, null);
        }
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
        string question, AiProvider provider, IReadOnlyList<ChatTurn> history, CancellationToken ct)
    {
        if (!provider.HasKey)
        {
            return (null, AiFailure.InvalidKey, null);
        }

        try
        {
            var system =
                "You are helping someone REHEARSE for a technical interview so they truly learn the " +
                "topic. Answer the way a hands-on Senior Software Engineer with almost 8 years of " +
                "real project experience would explain it in an interview \u2014 confident, practical, " +
                "and grounded in things actually seen while building and shipping software.\n" +
                "- Speak from experience: use a natural first-person voice (e.g. 'In my experience', " +
                "'What I usually do', 'On real projects I've seen') where it fits, without overusing it.\n" +
                "- Show maturity: mention the practical trade-off, the common mistake to avoid, or the " +
                "best practice a senior engineer would call out \u2014 not just the textbook definition.\n" +
                "- Sound senior but stay humble and clear; no buzzword stuffing.\n" +
                "ACCURACY IS THE TOP PRIORITY. Follow these rules strictly:\n" +
                "- Be technically precise and factually correct; use current, widely-accepted best " +
                "practices and correct terminology.\n" +
                "- Do NOT invent facts, APIs, numbers, or behavior. If something depends on a version, " +
                "language, or context, say so briefly.\n" +
                "- If you are not sure or the question is ambiguous, state the assumption you are making " +
                "in one short clause instead of guessing.\n" +
                "- Prefer concrete, verifiable details over vague generalities, but keep it concise.\n" +
                "Structure your reply in THREE parts, EXACTLY in this order and format:\n" +
                "1) FIRST line must start with 'In short:' followed by ONE short, direct, simple " +
                "sentence answering the question \u2014 the quick version they can say immediately.\n" +
                "2) THEN a SHORT explanation as 3 to 4 numbered points, each on its OWN line " +
                "beginning with '1.', '2.', '3.' and so on. Keep EACH point to ONE short sentence " +
                "(about 12-20 words) covering one key idea \u2014 what it is, why it matters, how it " +
                "works, or a trade-off / best practice. Do NOT write long paragraphs.\n" +
                "3) FINALLY one line that starts with 'Real example:' giving ONE short, concrete, " +
                "accurate real-world example (one sentence), ideally phrased like something from an " +
                "actual project (e.g. 'On a recent project I...') that shows the concept in action.\n" +
                "CODING / QUERY QUESTIONS: If the question asks you to WRITE or SHOW code, a SQL " +
                "query, a snippet, or 'how do you implement/write' something, then ADAPT the middle " +
                "part: keep the 'In short:' line, then put the ACTUAL, correct, runnable code inside a " +
                "fenced code block using triple backticks (```), properly indented \u2014 not prose. " +
                "After the code, add 1 to 3 short numbered points explaining the key parts, then the " +
                "'Real example:' line. Give real working code, never vague pseudo-code unless asked.\n" +
                "Keep the whole answer compact so it fits on one screen without scrolling. " +
                "Speak in a natural first-person tone (not a dry textbook) and use simple, clear " +
                "English because the person is not a native speaker. Do not add any other headings.";

            // Build the message list: system prompt, then any earlier conversation
            // (so follow-up questions keep context), then the new question.
            var messages = new List<object>
            {
                new { role = "system", content = system },
            };
            foreach (var turn in history)
            {
                messages.Add(new { role = turn.Role, content = turn.Content });
            }

            messages.Add(new { role = "user", content = question });

            var payload = new
            {
                model = provider.Model,
                messages = messages.ToArray(),
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
