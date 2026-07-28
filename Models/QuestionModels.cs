namespace InterviewPrep.Models;

/// <summary>Difficulty level for a question.</summary>
public enum Level
{
    Easy,
    Medium,
    Hard,
}

/// <summary>
/// One interview / discussion question with a strong model answer and the key
/// points a good answer should mention (used to score your own answer).
/// <paramref name="SimpleAnswer"/> is a short, easy-English version to say aloud.
/// </summary>
public sealed record Question(
    int Id,
    string Topic,
    Level Level,
    string Prompt,
    string ModelAnswer,
    string SimpleAnswer,
    IReadOnlyList<string> KeyPoints);

/// <summary>Result of scoring a user's answer against a question's key points.</summary>
public sealed record Feedback(
    Question Question,
    string UserAnswer,
    int ScorePercent,
    IReadOnlyList<string> CoveredPoints,
    IReadOnlyList<string> MissedPoints,
    string Comment);
