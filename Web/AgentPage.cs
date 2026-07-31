using System.Net;
using System.Text;
using InterviewPrep.Infrastructure;
using InterviewPrep.Services;

namespace InterviewPrep.Web;

/// <summary>Renders the "Agent mode" page — a CLI-style panel that writes code
/// changes directly into the project.</summary>
internal static class AgentPage
{
    public static string Render(
        string? task, string? projectName, string? location, string? message, IReadOnlyList<CodeAgent.AgentFileResult>? files,
        IReadOnlyList<AiProvider> models, string? selectedModel, string? notice = null,
        string? source = null, string? projectFolder = null)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Agent mode</title>");
        AppendStyles(sb);
        sb.Append("</head><body>");

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83e\udd16</span><div>");
        sb.Append("<div class=\"brand-name\">Agent mode</div>");
        sb.Append("<div class=\"brand-tag\">Name a project \u00b7 describe it \u00b7 the agent creates a new folder and writes the whole project</div>");
        sb.Append("</div></div>");
        sb.Append("<span class=\"mode\">builds projects</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip active\" href=\"/agent\">\ud83e\udd16 Agent mode</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        sb.Append("<div class=\"warn\">\u26a0\ufe0f This creates a <b>new project folder</b> at the location you choose and " +
                  "writes real files into it. It will never write into this app's own project. Review the code before you run it.</div>");

        if (!string.IsNullOrWhiteSpace(notice))
        {
            sb.Append($"<div class=\"notice\">{WebUtility.HtmlEncode(notice)}</div>");
        }

        sb.Append("<form method=\"post\" action=\"/agent\">");
        var priorName = projectName ?? string.Empty;
        sb.Append($"<input class=\"pname\" name=\"project\" id=\"project\" value=\"{WebUtility.HtmlEncode(priorName)}\" placeholder=\"New project folder name (e.g. TodoApp)\" autocomplete=\"off\">");
        var priorLoc = location ?? string.Empty;
        sb.Append($"<input class=\"pname\" name=\"location\" id=\"location\" value=\"{WebUtility.HtmlEncode(priorLoc)}\" placeholder=\"Where to create it \u2014 leave empty for Desktop, or type a path e.g. C:\\Projects\" autocomplete=\"off\">");
        var prior = task ?? string.Empty;
        sb.Append($"<textarea class=\"q\" name=\"task\" id=\"task\" rows=\"4\" placeholder=\"Describe the project to build, e.g. A C# .NET 8 console app that plays a number-guessing game, or a small Node.js REST API for a todo list\">{WebUtility.HtmlEncode(prior)}</textarea>");
        AppendModelPicker(sb, models, selectedModel);
        sb.Append("<div class=\"actions\">");
        sb.Append("<button class=\"btn btn-primary\" type=\"submit\">\u25b6 Build project</button>");
        sb.Append("<a class=\"btn\" href=\"/download-agent\" download>\u2b07 Download agent .exe (run in cmd)</a>");
        sb.Append("</div>");
        sb.Append("</form>");

        // CLI-style console output.
        sb.Append("<div class=\"console\">");
        sb.Append("<div class=\"cbar\"><span class=\"dot r\"></span><span class=\"dot y\"></span><span class=\"dot g\"></span><span class=\"ctitle\">agent \u2014 output</span></div>");
        sb.Append("<div class=\"cbody\">");
        if (files is null && string.IsNullOrWhiteSpace(message))
        {
            sb.Append("<div class=\"cline dim\">$ waiting for a task\u2026 name a project, describe it, and press Build project.</div>");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(task))
            {
                sb.Append($"<div class=\"cline\"><span class=\"p\">$</span> {WebUtility.HtmlEncode(task)}</div>");
            }

            if (!string.IsNullOrWhiteSpace(source))
            {
                sb.Append($"<div class=\"cline dim\">agent \u00b7 {WebUtility.HtmlEncode(source)}</div>");
            }

            if (!string.IsNullOrWhiteSpace(projectFolder) && files is { Count: > 0 })
            {
                sb.Append($"<div class=\"cline dim\">\ud83d\udcc1 {WebUtility.HtmlEncode(projectFolder)}</div>");
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.Append($"<div class=\"cline\">\u2713 {WebUtility.HtmlEncode(message)}</div>");
            }

            if (files is { Count: > 0 })
            {
                foreach (var f in files)
                {
                    var cls = f.Status.StartsWith("blocked") || f.Status.StartsWith("error") ? "bad" : "ok";
                    sb.Append($"<div class=\"cline {cls}\">{WebUtility.HtmlEncode(f.Status)} \u2014 {WebUtility.HtmlEncode(f.Path)}</div>");
                }

                sb.Append("<div class=\"cline dim\">Done. Open the folder above to run the new project.</div>");
            }
            else if (string.IsNullOrWhiteSpace(notice))
            {
                sb.Append("<div class=\"cline dim\">No files were changed.</div>");
            }
        }

        sb.Append("</div></div>");

        sb.Append("<p class=\"foot\">Agent mode writes code straight into the project \u2014 review changes before you rely on them.</p>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendModelPicker(
        StringBuilder sb, IReadOnlyList<AiProvider> models, string? selectedModel)
    {
        if (models is null || models.Count == 0)
        {
            return;
        }

        sb.Append("<div class=\"modelrow\">");
        sb.Append("<span class=\"mlabel\">\ud83e\udde0 Use model</span>");
        sb.Append("<select class=\"model\" name=\"model\">");
        foreach (var m in models)
        {
            var sel = string.Equals(m.Id, selectedModel, StringComparison.OrdinalIgnoreCase)
                ? " selected"
                : string.Empty;
            var disabled = m.HasKey ? string.Empty : " disabled";
            var label = m.HasKey ? m.DisplayName : m.DisplayName + " \u2014 add API key";
            sb.Append($"<option value=\"{WebUtility.HtmlEncode(m.Id)}\"{sel}{disabled}>{WebUtility.HtmlEncode(label)}</option>");
        }

        sb.Append("</select></div>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#4338ca,#0891b2);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:820px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;letter-spacing:-.3px;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".mode{background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.35);padding:6px 12px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap;}");
        sb.Append(".wrap{max-width:820px;margin:-14px auto 40px;padding:0 24px;}");
        sb.Append(".nav{display:flex;gap:8px;margin:22px 0 16px;flex-wrap:wrap;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#4338ca;color:#4338ca;}");
        sb.Append(".chip.active{background:#4338ca;border-color:#4338ca;color:#fff;}");
        sb.Append(".warn{background:#fef2f2;border:1px solid #fecaca;color:#991b1b;border-radius:12px;padding:12px 16px;font-size:13px;font-weight:600;line-height:1.55;margin-bottom:14px;}");
        sb.Append(".notice{background:#fef3c7;border:1px solid #fcd34d;color:#92400e;border-radius:12px;padding:12px 16px;font-size:13.5px;font-weight:600;line-height:1.55;margin-bottom:14px;}");
        sb.Append(".q{width:100%;border:1px solid #cbd5e1;border-radius:12px;padding:13px 15px;font-size:15px;font-family:inherit;resize:vertical;}");
        sb.Append(".q:focus{outline:none;border-color:#4338ca;box-shadow:0 0 0 3px rgba(67,56,202,.15);}");
        sb.Append(".pname{width:100%;border:1px solid #cbd5e1;border-radius:12px;padding:12px 15px;font-size:15px;font-family:inherit;font-weight:600;margin-bottom:10px;}");
        sb.Append(".pname:focus{outline:none;border-color:#4338ca;box-shadow:0 0 0 3px rgba(67,56,202,.15);}");
        sb.Append(".modelrow{display:flex;align-items:center;gap:10px;margin-top:12px;flex-wrap:wrap;}");
        sb.Append(".mlabel{font-size:13px;font-weight:700;color:#334155;}");
        sb.Append(".model{border:1px solid #cbd5e1;border-radius:10px;padding:9px 12px;font-size:14px;font-family:inherit;font-weight:600;color:#0f172a;background:#fff;cursor:pointer;}");
        sb.Append(".actions{margin-top:12px;}");
        sb.Append(".btn{border:none;border-radius:12px;padding:12px 18px;font-size:14.5px;font-weight:700;font-family:inherit;cursor:pointer;}");
        sb.Append(".btn-primary{background:#4338ca;color:#fff;}.btn-primary:hover{background:#3730a3;}");
        sb.Append(".console{margin-top:18px;background:#0f172a;border-radius:14px;overflow:hidden;box-shadow:0 6px 18px rgba(15,23,42,.18);}");
        sb.Append(".cbar{display:flex;align-items:center;gap:8px;padding:10px 14px;background:#1e293b;}");
        sb.Append(".dot{width:11px;height:11px;border-radius:50%;display:inline-block;}");
        sb.Append(".dot.r{background:#ef4444;}.dot.y{background:#f59e0b;}.dot.g{background:#22c55e;}");
        sb.Append(".ctitle{margin-left:8px;color:#94a3b8;font-size:12px;font-weight:600;}");
        sb.Append(".cbody{padding:16px 18px;font-family:Consolas,'Courier New',monospace;font-size:13.5px;line-height:1.7;color:#e2e8f0;max-height:460px;overflow:auto;}");
        sb.Append(".cline{white-space:pre-wrap;word-break:break-word;}");
        sb.Append(".cline .p{color:#38bdf8;}");
        sb.Append(".cline.dim{color:#64748b;}");
        sb.Append(".cline.ok{color:#4ade80;}");
        sb.Append(".cline.bad{color:#f87171;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("</style>");
    }
}
