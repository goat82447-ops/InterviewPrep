using System.Text;
using System.Text.RegularExpressions;
using InterviewPrep.Models;

namespace InterviewPrep.Services;

/// <summary>
/// Scores a user's own answer against a question's key points using simple,
/// transparent keyword coverage. No network needed — this always works and
/// teaches you which important points you missed.
/// </summary>
public sealed class AnswerScorer
{
    public Feedback Score(Question question, string userAnswer)
    {
        userAnswer ??= string.Empty;
        var normalized = Normalize(userAnswer);
        var words = new HashSet<string>(
            Tokenize(normalized),
            StringComparer.OrdinalIgnoreCase);

        var covered = new List<string>();
        var missed = new List<string>();

        foreach (var point in question.KeyPoints)
        {
            if (Mentions(normalized, words, point))
            {
                covered.Add(point);
            }
            else
            {
                missed.Add(point);
            }
        }

        var total = question.KeyPoints.Count;
        var score = total == 0 ? 0 : (int)Math.Round(100.0 * covered.Count / total);

        // Penalize an answer that is far too short to be a real explanation.
        var wordCount = Tokenize(normalized).Count();
        if (wordCount < 8 && score > 40)
        {
            score = 40;
        }

        return new Feedback(question, userAnswer, score, covered, missed, BuildComment(score, missed));
    }

    private static bool Mentions(string normalizedAnswer, HashSet<string> words, string point)
    {
        var key = point.Trim().ToLowerInvariant();

        // Multi-word key point: look for the whole phrase.
        if (key.Contains(' '))
        {
            return normalizedAnswer.Contains(key, StringComparison.OrdinalIgnoreCase);
        }

        // Single word: match the token or a close stem (plural / verb ending).
        if (words.Contains(key))
        {
            return true;
        }

        foreach (var w in words)
        {
            if (w.StartsWith(key, StringComparison.OrdinalIgnoreCase) &&
                w.Length - key.Length <= 3)
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string text) =>
        Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9 ]", " ");

    private static IEnumerable<string> Tokenize(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string BuildComment(int score, IReadOnlyList<string> missed)
    {
        var sb = new StringBuilder();
        if (score >= 80)
        {
            sb.Append("Excellent — you covered the key ideas clearly.");
        }
        else if (score >= 55)
        {
            sb.Append("Good answer. You've got the main idea; tighten it up.");
        }
        else if (score >= 30)
        {
            sb.Append("Partial answer. You're on the right track but missing important points.");
        }
        else
        {
            sb.Append("Keep practicing — try to include the core concepts below.");
        }

        if (missed.Count > 0)
        {
            sb.Append(" Try to also mention: ");
            sb.Append(string.Join(", ", missed));
            sb.Append('.');
        }

        return sb.ToString();
    }
}
