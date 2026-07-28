using System.Net;
using System.Text;

namespace InterviewPrep.Web;

/// <summary>Renders answer text as HTML in three visual parts: a short direct
/// answer at the top, the fuller numbered explanation in the middle, and a real
/// example at the bottom \u2014 each styled distinctly so they stand apart.</summary>
internal static class AnswerFormat
{
    // Markers the AI is asked to emit. We also accept older/synonym markers so
    // previously-generated answers still render nicely.
    private static readonly string[] ShortMarkers = { "In short:", "Say it simply:" };
    private static readonly string[] ExampleMarkers = { "Real example:", "Real-time example:", "Real world example:", "Example:" };

    public static string ToHtml(string? answer)
    {
        var text = (answer ?? string.Empty).Replace("\r\n", "\n").Trim();

        // 1) Pull out the "Real example:" section (from the marker to the end).
        string? example = null;
        var exIdx = LastMarker(text, ExampleMarkers, out var exLen);
        if (exIdx >= 0)
        {
            example = text[(exIdx + exLen)..].Trim();
            text = text[..exIdx].Trim();
        }

        // 2) Pull out the short answer ("In short:" / "Say it simply:") \u2014 just its line.
        string? shortAnswer = null;
        var shIdx = FirstMarker(text, ShortMarkers, out var shLen);
        if (shIdx >= 0)
        {
            var start = shIdx + shLen;
            var nl = text.IndexOf('\n', start);
            if (nl < 0)
            {
                shortAnswer = text[start..].Trim();
                text = text[..shIdx].Trim();
            }
            else
            {
                shortAnswer = text[start..nl].Trim();
                text = (text[..shIdx] + "\n" + text[(nl + 1)..]).Trim();
            }
        }

        var sb = new StringBuilder();

        // Short answer first.
        if (!string.IsNullOrEmpty(shortAnswer))
        {
            sb.Append("<div class=\"shortline\"><span class=\"shortlabel\">\u26a1 In short:</span> ");
            sb.Append($"<span class=\"shorttext\">{WebUtility.HtmlEncode(shortAnswer)}</span></div>");
        }

        // Fuller explanation in the middle.
        AppendParagraphs(sb, text);

        // Real example last.
        if (!string.IsNullOrEmpty(example))
        {
            sb.Append("<div class=\"exline\"><span class=\"exlabel\">\ud83d\udca1 Real example:</span> ");
            sb.Append($"<span class=\"extext\">{WebUtility.HtmlEncode(example)}</span></div>");
        }

        return sb.ToString();
    }

    /// <summary>Finds the earliest occurrence of any marker; returns its index
    /// and the matched marker's length, or -1 if none are present.</summary>
    private static int FirstMarker(string text, string[] markers, out int matchedLength)
    {
        var best = -1;
        matchedLength = 0;
        foreach (var m in markers)
        {
            var i = text.IndexOf(m, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best < 0 || i < best))
            {
                best = i;
                matchedLength = m.Length;
            }
        }

        return best;
    }

    /// <summary>Finds the latest occurrence of any marker; returns its index and
    /// the matched marker's length, or -1 if none are present.</summary>
    private static int LastMarker(string text, string[] markers, out int matchedLength)
    {
        var best = -1;
        matchedLength = 0;
        foreach (var m in markers)
        {
            var i = text.LastIndexOf(m, StringComparison.OrdinalIgnoreCase);
            if (i > best)
            {
                best = i;
                matchedLength = m.Length;
            }
        }

        return best;
    }

    private static void AppendParagraphs(StringBuilder sb, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var inList = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (IsNumberedLine(line, out var body))
            {
                if (!inList)
                {
                    sb.Append("<ol class=\"apoints\">");
                    inList = true;
                }

                sb.Append($"<li>{WebUtility.HtmlEncode(body)}</li>");
            }
            else
            {
                if (inList)
                {
                    sb.Append("</ol>");
                    inList = false;
                }

                sb.Append($"<div class=\"aline\">{WebUtility.HtmlEncode(line)}</div>");
            }
        }

        if (inList)
        {
            sb.Append("</ol>");
        }
    }

    /// <summary>True when a line looks like a numbered point (e.g. "1." or "2)");
    /// returns the text after the number.</summary>
    private static bool IsNumberedLine(string line, out string body)
    {
        body = string.Empty;
        var i = 0;
        while (i < line.Length && char.IsDigit(line[i]))
        {
            i++;
        }

        if (i == 0 || i >= line.Length || (line[i] != '.' && line[i] != ')'))
        {
            return false;
        }

        body = line[(i + 1)..].Trim();
        return body.Length > 0;
    }

    /// <summary>Shared CSS for the three answer parts: the short answer box, the
    /// numbered explanation, and the real-example box \u2014 each visually distinct.</summary>
    public static void AppendSayStyles(StringBuilder sb)
    {
        sb.Append(".aline{margin:0 0 6px;}");
        sb.Append(".apoints{margin:8px 0 4px;padding-left:24px;}");
        sb.Append(".apoints li{margin:0 0 10px;padding-left:5px;line-height:1.62;}");
        sb.Append(".apoints li::marker{color:#0891b2;font-weight:800;}");

        // Short answer \u2014 blue box at the top.
        sb.Append(".shortline{margin:0 0 14px;background:#eff6ff;border-left:4px solid #3b82f6;");
        sb.Append("border-radius:10px;padding:12px 14px;font-size:16px;line-height:1.55;color:#1e3a8a;}");
        sb.Append(".shortlabel{font-weight:700;color:#1d4ed8;letter-spacing:.2px;margin-right:4px;}");
        sb.Append(".shorttext{font-weight:600;}");

        // Real example \u2014 amber box at the bottom.
        sb.Append(".exline{margin-top:14px;background:#fffbeb;border-left:4px solid #f59e0b;");
        sb.Append("border-radius:10px;padding:12px 14px;font-family:Georgia,'Times New Roman',serif;");
        sb.Append("font-size:16px;line-height:1.55;color:#78350f;}");
        sb.Append(".exlabel{font-weight:700;color:#b45309;letter-spacing:.2px;margin-right:4px;}");
        sb.Append(".extext{font-style:italic;}");

        // Backward-compat: older answers that used the "Say it simply:" green box.
        sb.Append(".sayline{margin-top:14px;background:#ecfdf5;border-left:4px solid #10b981;");
        sb.Append("border-radius:10px;padding:12px 14px;font-family:Georgia,'Times New Roman',serif;");
        sb.Append("font-size:16px;line-height:1.55;color:#065f46;}");
        sb.Append(".saylabel{font-weight:700;color:#047857;letter-spacing:.2px;margin-right:4px;}");
        sb.Append(".saytext{font-style:italic;}");
    }
}
