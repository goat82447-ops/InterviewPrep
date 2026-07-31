using System.Net;
using System.Text;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;
using InterviewPrep.Services;

namespace InterviewPrep.Web;

/// <summary>Renders the "Mock interview" page: a realistic back-and-forth where
/// you answer, get coached, see a strong model answer, then get a follow-up.</summary>
internal static class MockPage
{
    public static string Render(
        string? topic, string question, MockTurn? turn, bool aiEnabled,
        IReadOnlyList<AiProvider> models, string? selectedModel)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Mock Interview</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83c\udf99\ufe0f</span><div>");
        sb.Append("<div class=\"brand-name\">Mock Interview</div>");
        sb.Append("<div class=\"brand-tag\">Answer out loud \u00b7 get coached \u00b7 face a real follow-up question</div>");
        sb.Append("</div></div>");
        sb.Append($"<span class=\"mode\">{(aiEnabled ? "AI interviewer: on" : "AI: off (study bank)")}</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip active\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        // Topic chips (restart the interview on a chosen topic)
        sb.Append("<div class=\"topics\">");
        var randomActive = string.IsNullOrWhiteSpace(topic) ? " active" : string.Empty;
        sb.Append($"<a class=\"chip{randomActive}\" href=\"/mock\">\ud83c\udfb2 Any topic</a>");
        foreach (var t in QuestionBank.Topics)
        {
            var active = string.Equals(t, topic, StringComparison.OrdinalIgnoreCase) ? " active" : string.Empty;
            sb.Append($"<a class=\"chip{active}\" href=\"/mock?topic={WebUtility.UrlEncode(t)}\">{WebUtility.HtmlEncode(t)}</a>");
        }

        sb.Append("</div>");

        // If we have a completed turn, show coaching + model answer first.
        if (turn is not null)
        {
            sb.Append("<section class=\"fb\">");
            sb.Append("<div class=\"src\">Interviewer feedback</div>");
            sb.Append($"<div class=\"fbtext\">{WebUtility.HtmlEncode(turn.Feedback)}</div>");
            sb.Append("</section>");

            sb.Append("<section class=\"acard\">");
            sb.Append("<div class=\"src\">How a strong candidate would answer</div>");
            sb.Append($"<div class=\"atext\">{AnswerFormat.ToHtml(turn.ModelAnswer)}</div>");
            sb.Append("</section>");
        }

        // The current question to answer (either the opener or the follow-up).
        sb.Append("<section class=\"qcard\">");
        sb.Append(turn is null
            ? "<div class=\"qlabel\">Your interviewer asks</div>"
            : "<div class=\"qlabel\">Follow-up question</div>");
        sb.Append($"<div class=\"qtext\">{WebUtility.HtmlEncode(question)}</div>");

        sb.Append("<form method=\"post\" action=\"/mock\">");
        sb.Append($"<input type=\"hidden\" name=\"topic\" value=\"{WebUtility.HtmlEncode(topic ?? string.Empty)}\">");
        sb.Append($"<input type=\"hidden\" name=\"question\" value=\"{WebUtility.HtmlEncode(question)}\">");
        sb.Append("<textarea class=\"q\" name=\"answer\" id=\"ans\" rows=\"4\" placeholder=\"Answer in your own words, the way you would say it out loud\u2026\"></textarea>");
        AppendModelPicker(sb, models, selectedModel);
        sb.Append("<div class=\"actions\">");
        sb.Append("<button class=\"btn btn-primary\" type=\"submit\">Submit answer</button>");
        sb.Append("<button class=\"btn btn-mic\" type=\"button\" id=\"micBtn\">\ud83c\udfa4 Speak your answer</button>");
        sb.Append("<span class=\"mic-status\" id=\"micStatus\"></span>");
        sb.Append("</div>");
        sb.Append("</form>");
        sb.Append("</section>");

        sb.Append("<p class=\"foot\">Practice out loud until the answers feel natural \u2014 that is what carries you through the real interview.</p>");
        AppendMicScript(sb);
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
        sb.Append("<span class=\"mlabel\">\ud83c\udf99\ufe0f Interviewer model</span>");
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

    private static void AppendMicScript(StringBuilder sb)
    {
        // No server, no keys, nothing recorded.
        sb.Append("<script>(function(){");
        sb.Append("var btn=document.getElementById('micBtn');");
        sb.Append("var status=document.getElementById('micStatus');");
        sb.Append("var box=document.getElementById('ans');");
        sb.Append("var SR=window.SpeechRecognition||window.webkitSpeechRecognition;");
        sb.Append("if(!SR){if(btn){btn.disabled=true;btn.textContent='\ud83c\udfa4 Speech not supported in this browser';}return;}");
        sb.Append("var rec=new SR();rec.lang='en-US';rec.interimResults=true;rec.continuous=false;");
        sb.Append("var listening=false;var base='';");
        sb.Append("btn.addEventListener('click',function(){if(listening){rec.stop();return;}base=box.value?box.value+' ':'';try{rec.start();}catch(e){}});");
        sb.Append("rec.onstart=function(){listening=true;btn.textContent='\u23f9 Stop listening';status.textContent='Listening\u2026 speak your answer';};");
        sb.Append("rec.onerror=function(e){status.textContent='Mic error: '+e.error;};");
        sb.Append("rec.onend=function(){listening=false;btn.textContent='\ud83c\udfa4 Speak your answer';if(!status.textContent.startsWith('Mic error'))status.textContent='';};");
        sb.Append("rec.onresult=function(ev){var t='';for(var i=0;i<ev.results.length;i++){t+=ev.results[i][0].transcript;}box.value=base+t;};");
        sb.Append("})();</script>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#b45309,#db2777);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:820px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;letter-spacing:-.3px;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".mode{background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.35);padding:6px 12px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap;}");
        sb.Append(".wrap{max-width:820px;margin:-14px auto 40px;padding:0 24px;}");
        sb.Append(".nav{display:flex;gap:8px;margin:22px 0 8px;flex-wrap:wrap;}");
        sb.Append(".topics{display:flex;gap:8px;flex-wrap:wrap;margin:8px 0 18px;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#db2777;color:#db2777;}");
        sb.Append(".chip.active{background:#db2777;border-color:#db2777;color:#fff;}");
        sb.Append(".qcard{background:#fff;border-radius:16px;padding:20px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-top:8px;}");
        sb.Append(".qlabel{font-size:11.5px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;color:#db2777;margin-bottom:8px;}");
        sb.Append(".qtext{font-size:18px;font-weight:600;line-height:1.5;margin-bottom:14px;}");
        sb.Append(".q{width:100%;border:1px solid #cbd5e1;border-radius:12px;padding:13px 15px;font-size:15px;font-family:inherit;resize:vertical;}");
        sb.Append(".q:focus{outline:none;border-color:#db2777;box-shadow:0 0 0 3px rgba(219,39,119,.15);}");
        sb.Append(".actions{margin-top:12px;}");
        sb.Append(".modelrow{display:flex;align-items:center;gap:10px;margin-top:12px;flex-wrap:wrap;}");
        sb.Append(".mlabel{font-size:13px;font-weight:700;color:#334155;}");
        sb.Append(".model{border:1px solid #cbd5e1;border-radius:10px;padding:9px 12px;font-size:14px;font-family:inherit;font-weight:600;color:#0f172a;background:#fff;cursor:pointer;}");
        sb.Append(".model:focus{outline:none;border-color:#db2777;box-shadow:0 0 0 3px rgba(219,39,119,.15);}");
        sb.Append(".btn{border:none;border-radius:12px;padding:12px 18px;font-size:14.5px;font-weight:700;font-family:inherit;cursor:pointer;}");
        sb.Append(".btn-primary{background:#db2777;color:#fff;}.btn-primary:hover{background:#be185d;}");
        sb.Append(".btn-mic{background:#f1f5f9;color:#334155;margin-left:8px;}.btn-mic:hover{background:#e2e8f0;}");
        sb.Append(".btn-mic:disabled{opacity:.6;cursor:not-allowed;}");
        sb.Append(".mic-status{margin-left:10px;font-size:13px;color:#db2777;font-weight:600;}");
        sb.Append(".fb{background:#fff7ed;border:1px solid #fed7aa;border-radius:16px;padding:16px 20px;margin-top:8px;}");
        sb.Append(".fbtext{font-size:15px;line-height:1.6;color:#7c2d12;}");
        sb.Append(".acard{background:#fff;border-radius:16px;padding:22px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-top:14px;border-left:5px solid #10b981;}");
        sb.Append(".src{font-size:11.5px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;color:#db2777;margin-bottom:10px;}");
        sb.Append(".atext{font-size:17.5px;line-height:1.75;color:#0f172a;}");
        AnswerFormat.AppendSayStyles(sb);
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
