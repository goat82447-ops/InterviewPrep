using System.Net;
using System.Text;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Web;

/// <summary>Renders the "Settings" page where the user pastes one API key per
/// provider (Groq, Gemini, OpenRouter, NVIDIA, OpenAI). Keys are written to a
/// local per-user file and never displayed back or sent anywhere except the AI
/// provider itself. Existing keys show only a "saved" badge, not the value.</summary>
internal static class SettingsPage
{
    private sealed record VendorInfo(string Id, string Name, string Where, string Url);

    private static readonly VendorInfo[] Vendors =
    {
        new("groq", "Groq", "console.groq.com/keys", "https://console.groq.com/keys"),
        new("gemini", "Google Gemini", "aistudio.google.com/apikey", "https://aistudio.google.com/apikey"),
        new("openrouter", "OpenRouter", "openrouter.ai/keys", "https://openrouter.ai/keys"),
        new("nvidia", "NVIDIA", "build.nvidia.com", "https://build.nvidia.com"),
        new("openai", "OpenAI", "platform.openai.com/api-keys", "https://platform.openai.com/api-keys"),
    };

    public static string Render(AppConfig config, string? savedMessage)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Settings</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");
        WebChrome.Append(sb);

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\u2699\ufe0f</span><div>");
        sb.Append("<div class=\"brand-name\">Settings</div>");
        sb.Append("<div class=\"brand-tag\">Paste your API keys once \u00b7 stored only on this computer \u00b7 turns the AI features on</div>");
        sb.Append("</div></div>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/interview\">\ud83e\udde9 Interview mode</a>");
        sb.Append("<a class=\"chip\" href=\"/dashboard\">\ud83d\udcc8 Progress</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip active\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        if (!string.IsNullOrWhiteSpace(savedMessage))
        {
            sb.Append($"<div class=\"saved\">\u2705 {WebUtility.HtmlEncode(savedMessage)}</div>");
        }

        sb.Append("<div class=\"note\">Your keys are saved locally on this computer at " +
                  $"<code>{WebUtility.HtmlEncode(KeyStore.ConfigPath)}</code>. " +
                  "They are never shown back to you and never sent anywhere except the AI provider you use. " +
                  "You only need one key for any single provider to unlock its models.</div>");

        sb.Append("<form method=\"post\" action=\"/settings\" autocomplete=\"off\">");

        foreach (var v in Vendors)
        {
            var hasKey = config.Providers.Any(p =>
                string.Equals(p.Id, v.Id, StringComparison.OrdinalIgnoreCase) && p.HasKey);

            sb.Append("<div class=\"card\">");
            sb.Append("<div class=\"card-head\">");
            sb.Append($"<span class=\"vname\">{WebUtility.HtmlEncode(v.Name)}</span>");
            sb.Append(hasKey
                ? "<span class=\"badge on\">\u25cf saved</span>"
                : "<span class=\"badge off\">not set</span>");
            sb.Append("</div>");

            var placeholder = hasKey
                ? "A key is already saved \u2014 leave blank to keep it, or paste a new one to replace it."
                : "Paste your API key here";
            sb.Append($"<input class=\"key\" type=\"password\" name=\"{WebUtility.HtmlEncode(v.Id)}\" " +
                      $"placeholder=\"{WebUtility.HtmlEncode(placeholder)}\" autocomplete=\"off\" spellcheck=\"false\">");

            sb.Append($"<div class=\"help\">Get a free key at <a href=\"{v.Url}\" target=\"_blank\" rel=\"noopener\">{WebUtility.HtmlEncode(v.Where)}</a></div>");
            sb.Append("</div>");
        }

        sb.Append("<div class=\"actions\">");
        sb.Append("<button class=\"save-btn\" type=\"submit\">Save keys</button>");
        sb.Append("<span class=\"hint\">After saving, the new keys are picked up right away for the AI features.</span>");
        sb.Append("</div>");

        sb.Append("</form>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#4338ca,#0891b2);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:760px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".wrap{max-width:760px;margin:auto;padding:18px 18px 60px;}");
        sb.Append(".nav{display:flex;flex-wrap:wrap;gap:8px;margin:14px 0;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".chip.active{background:#0891b2;border-color:#0891b2;color:#fff;}");
        sb.Append(".saved{background:#ecfdf5;border:1px solid #a7f3d0;color:#065f46;border-radius:12px;padding:12px 16px;font-weight:600;margin-bottom:12px;}");
        sb.Append(".note{background:#eef2ff;border:1px solid #c7d2fe;border-radius:12px;padding:12px 16px;font-size:13.5px;line-height:1.5;color:#3730a3;margin-bottom:16px;}");
        sb.Append(".note code{background:#e0e7ff;padding:2px 6px;border-radius:6px;font-size:12px;word-break:break-all;}");
        sb.Append(".card{background:#fff;border-radius:14px;padding:16px 18px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-bottom:12px;}");
        sb.Append(".card-head{display:flex;align-items:center;gap:10px;margin-bottom:10px;}");
        sb.Append(".vname{font-size:16px;font-weight:800;}");
        sb.Append(".badge{font-size:11px;font-weight:800;padding:3px 10px;border-radius:999px;text-transform:uppercase;letter-spacing:.03em;}");
        sb.Append(".badge.on{background:#10b981;color:#fff;}.badge.off{background:#e2e8f0;color:#64748b;}");
        sb.Append(".key{width:100%;border:1px solid #cbd5e1;border-radius:10px;padding:11px 13px;font-size:14px;font-family:inherit;background:#f8fafc;}");
        sb.Append(".key:focus{outline:none;border-color:#0891b2;box-shadow:0 0 0 3px rgba(8,145,178,.15);background:#fff;}");
        sb.Append(".help{font-size:12.5px;color:#64748b;margin-top:7px;}.help a{color:#0891b2;font-weight:700;text-decoration:none;}.help a:hover{text-decoration:underline;}");
        sb.Append(".actions{display:flex;align-items:center;gap:14px;flex-wrap:wrap;margin-top:8px;}");
        sb.Append(".save-btn{background:#4338ca;color:#fff;border:none;border-radius:10px;padding:12px 22px;font-size:15px;font-weight:700;font-family:inherit;cursor:pointer;}");
        sb.Append(".save-btn:hover{background:#3730a3;}");
        sb.Append(".hint{font-size:12.5px;color:#64748b;}");
        sb.Append("@media(max-width:560px){.hero{padding:20px 16px;}.wrap{padding:14px 12px 48px;}}");
        sb.Append("</style>");
    }
}
