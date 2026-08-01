using System.Text;

namespace InterviewPrep.Infrastructure;

/// <summary>
/// Loads the ANSWER_STYLE.md guide once and exposes it as a reusable system-prompt
/// prefix so every AI answer (Ask mode, interview answers, and Agent/code mode)
/// follows the same "8-years tech lead, short, correct, human" voice. If the file
/// is missing it returns an empty string so callers work unchanged.
/// </summary>
public static class AnswerStyle
{
    private static readonly Lazy<string> Cached = new(LoadFile);

    /// <summary>The full ANSWER_STYLE.md text (empty if the file is missing).</summary>
    public static string Guide => Cached.Value;

    /// <summary>
    /// Prepends the shared style guide to a service's own system prompt. Returns
    /// the original prompt unchanged when the guide file is missing.
    /// </summary>
    public static string Wrap(string systemPrompt)
    {
        var guide = Guide;
        if (string.IsNullOrWhiteSpace(guide))
        {
            return systemPrompt;
        }

        return
            "Follow this ANSWER STYLE GUIDE for how you write (voice, length, and code quality). " +
            "It overrides any conflicting formatting habit:\n\n" +
            guide +
            "\n\n---\n\n" +
            systemPrompt;
    }

    private static string LoadFile()
    {
        try
        {
            var path = Path.Combine(ProjectPaths.ProjectRoot, "ANSWER_STYLE.md");
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
