using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InterviewPrep.Infrastructure;
using InterviewPrep.Models;

namespace InterviewPrep.Services;

/// <summary>
/// Optional AI coach. When an OpenAI key is configured it asks the model for a
/// short, encouraging critique of your answer plus one tip. Entirely optional —
/// callers should only use it when <see cref="AppConfig.HasOpenAi"/> is true.
/// </summary>
public sealed class OpenAiCoach : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;

    public OpenAiCoach(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Returns a short coaching note, or null if OpenAI is unavailable/fails.
    /// </summary>
    public async Task<string?> CritiqueAsync(Question question, string userAnswer, CancellationToken ct = default)
    {
        if (!_config.HasOpenAi)
        {
            return null;
        }

        try
        {
            var system =
                "You are a friendly technical interview coach. The user is not a native " +
                "English speaker. In 2-3 short sentences, give warm, specific feedback on their " +
                "answer, then one concrete tip to improve it. Be encouraging and simple. " +
                "Base your feedback strictly on the model answer provided; if the user's answer " +
                "is technically wrong or incomplete, gently point out the correct fact instead of " +
                "praising an incorrect statement. Do not invent details not implied by the model answer.";

            var user =
                $"Question: {question.Prompt}\n" +
                $"Model answer: {question.ModelAnswer}\n" +
                $"My answer: {userAnswer}";

            var payload = new
            {
                model = _config.OpenAiModel,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                temperature = 0.2,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _config.OpenAiBaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.OpenAiApiKey);
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
            // Any failure (network, quota, parsing) -> silently skip AI coaching.
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
