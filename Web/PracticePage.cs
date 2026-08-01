using System.Net;
using System.Text;
using InterviewPrep.Data;
using InterviewPrep.Models;

namespace InterviewPrep.Web;

/// <summary>Renders the Interview Practice dashboard HTML.</summary>
internal static class PracticePage
{
    public static string Render(
        string? topic,
        Question? question,
        Feedback? feedback,
        string? aiNote,
        bool aiEnabled,
        PracticeSituation? situation = null,
        string? notice = null)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Interview Practice</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");
        WebChrome.Append(sb);

        // Header
        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83c\udf93</span><div>");
        sb.Append("<div class=\"brand-name\">Interview Practice</div>");
        sb.Append("<div class=\"brand-tag\">Practice technical questions \u00b7 answer in your words \u00b7 learn the strong answer</div>");
        sb.Append("</div></div>");
        var badge = aiEnabled ? "AI coach: on" : "AI coach: off";
        sb.Append($"<span class=\"mode\">{badge}</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip active\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/interview\">\ud83e\udde9 Interview mode</a>");
        sb.Append("<a class=\"chip\" href=\"/dashboard\">\ud83d\udcc8 Progress</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        // Topic chips
        sb.Append("<div class=\"topics\">");
        foreach (var t in QuestionBank.Topics)
        {
            var active = string.Equals(t, topic, StringComparison.OrdinalIgnoreCase) ? " active" : string.Empty;
            sb.Append($"<a class=\"chip{active}\" href=\"/practice?topic={WebUtility.UrlEncode(t)}\">{WebUtility.HtmlEncode(t)}</a>");
        }
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udfb2 Random</a>");
        sb.Append("</div>");

        AppendGenPanel(sb, situation, aiEnabled);

        if (!string.IsNullOrWhiteSpace(notice))
        {
            sb.Append($"<div class=\"gp-notice\">{WebUtility.HtmlEncode(notice)}</div>");
        }

        if (question is null)
        {
            sb.Append("<div class=\"empty\"><div class=\"empty-emoji\">\ud83d\udcac</div><h3>Describe your situation above, or pick a topic</h3>");
            sb.Append("<p>Use \u2728 <b>Generate a question with AI</b> to get a fresh question made just for your topic, " +
                      "role or job description \u2014 with a strong model answer. Or pick a topic chip for a ready-made question.</p></div>");
            sb.Append("</main></body></html>");
            return sb.ToString();
        }

        // Question card
        sb.Append("<section class=\"qcard\">");
        sb.Append("<div class=\"qmeta\">");
        sb.Append($"<span class=\"tag topic\">{WebUtility.HtmlEncode(question.Topic)}</span>");
        sb.Append($"<span class=\"tag lvl-{question.Level.ToString().ToLowerInvariant()}\">{question.Level}</span>");
        sb.Append("</div>");
        sb.Append($"<h2 class=\"qprompt\">{WebUtility.HtmlEncode(question.Prompt)}</h2>");

        // Answer form
        sb.Append("<form method=\"post\" action=\"/answer\">");
        sb.Append($"<input type=\"hidden\" name=\"id\" value=\"{question.Id}\">");
        AppendSituationFields(sb, situation);
        var prior = feedback?.UserAnswer ?? string.Empty;
        sb.Append($"<textarea class=\"answer\" name=\"answer\" id=\"ans\" rows=\"5\" placeholder=\"Type or speak your answer here in your own words...\">{WebUtility.HtmlEncode(prior)}</textarea>");
        sb.Append("<div class=\"actions\">");
        sb.Append("<button class=\"btn btn-primary\" type=\"submit\">Check my answer</button>");
        sb.Append("<button class=\"btn btn-mic\" type=\"button\" id=\"micBtn\">\ud83c\udfa4 Speak your answer</button>");
        if (situation is not null)
        {
            // AI question: "Next" regenerates a fresh one with the same situation.
            sb.Append("<button class=\"btn btn-ghost\" type=\"submit\" formaction=\"/practice/generate\">Next question \u2192</button>");
        }
        else
        {
            sb.Append($"<a class=\"btn btn-ghost\" href=\"/practice?topic={WebUtility.UrlEncode(question.Topic)}\">Next question \u2192</a>");
        }
        sb.Append("<span class=\"mic-status\" id=\"micStatus\"></span>");
        sb.Append("</div></form>");
        sb.Append("</section>");

        // Feedback
        if (feedback is not null)
        {
            var scoreCls = feedback.ScorePercent >= 80 ? "good" : feedback.ScorePercent >= 55 ? "ok" : "low";
            sb.Append("<section class=\"fb\">");
            sb.Append("<div class=\"fb-head\">");
            sb.Append($"<div class=\"score score-{scoreCls}\">{feedback.ScorePercent}%</div>");
            sb.Append($"<div class=\"fb-comment\">{WebUtility.HtmlEncode(feedback.Comment)}</div>");
            sb.Append("</div>");

            if (feedback.CoveredPoints.Count > 0)
            {
                sb.Append("<div class=\"points\"><b>\u2705 You mentioned:</b> ");
                sb.Append(string.Join(", ", feedback.CoveredPoints.Select(WebUtility.HtmlEncode)));
                sb.Append("</div>");
            }

            if (feedback.MissedPoints.Count > 0)
            {
                sb.Append("<div class=\"points miss\"><b>\ud83d\udca1 Add next time:</b> ");
                sb.Append(string.Join(", ", feedback.MissedPoints.Select(WebUtility.HtmlEncode)));
                sb.Append("</div>");
            }

            if (!string.IsNullOrWhiteSpace(aiNote))
            {
                sb.Append($"<div class=\"ainote\"><b>\ud83e\udd16 Coach:</b> {WebUtility.HtmlEncode(aiNote)}</div>");
            }

            sb.Append("<details class=\"model\" open><summary>\u2b50 Strong model answer</summary>");
            sb.Append($"<p>{WebUtility.HtmlEncode(question.ModelAnswer)}</p>");
            sb.Append($"<div class=\"simple\"><span class=\"slabel\">\ud83d\udde3 Say it simply:</span> <span class=\"stext\">{WebUtility.HtmlEncode(question.SimpleAnswer)}</span></div>");
            sb.Append("</details>");
            sb.Append("</section>");
        }

        sb.Append("<p class=\"foot\">Answer openly and honestly \u2014 this tool helps you learn and explain the material yourself.</p>");
        AppendMicScript(sb);
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendGenPanel(StringBuilder sb, PracticeSituation? s, bool aiEnabled)
    {
        var topic = WebUtility.HtmlEncode(s?.Topic ?? string.Empty);
        var role = WebUtility.HtmlEncode(s?.Role ?? string.Empty);
        var jd = WebUtility.HtmlEncode(s?.JobDescription ?? string.Empty);
        var diff = s?.Difficulty ?? "Any";

        sb.Append("<form class=\"genpanel\" method=\"post\" action=\"/practice/generate\">");
        sb.Append("<div class=\"gp-title\">\u2728 Generate a question with AI \u2014 describe your situation</div>");
        if (!aiEnabled)
        {
            sb.Append("<div class=\"gp-hint\">No AI key set yet \u2014 add a free one (see FREE_AI_KEYS.md) to turn this on.</div>");
        }

        sb.Append("<div class=\"gp-grid\">");
        sb.Append($"<label>Topic / skill<input name=\"gtopic\" value=\"{topic}\" placeholder=\"e.g. SQL joins, Azure, C# async\"></label>");
        sb.Append($"<label>Role / seniority<input name=\"grole\" value=\"{role}\" placeholder=\"e.g. Senior .NET developer\"></label>");
        sb.Append("<label>Difficulty<select name=\"gdiff\">");
        foreach (var opt in new[] { "Any", "Easy", "Medium", "Hard" })
        {
            var sel = opt.Equals(diff, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty;
            sb.Append($"<option value=\"{opt}\"{sel}>{opt}</option>");
        }
        sb.Append("</select></label>");
        sb.Append("</div>");

        sb.Append($"<label class=\"gp-jd\">Job description (optional \u2014 paste it)<textarea name=\"gjd\" rows=\"3\" placeholder=\"Paste the job description here...\">{jd}</textarea></label>");
        sb.Append("<button class=\"btn btn-primary gp-btn\" type=\"submit\">\u2728 Generate question</button>");
        sb.Append("</form>");
    }

    private static void AppendSituationFields(StringBuilder sb, PracticeSituation? s)
    {
        if (s is null)
        {
            return;
        }

        sb.Append($"<input type=\"hidden\" name=\"gtopic\" value=\"{WebUtility.HtmlEncode(s.Topic ?? string.Empty)}\">");
        sb.Append($"<input type=\"hidden\" name=\"grole\" value=\"{WebUtility.HtmlEncode(s.Role ?? string.Empty)}\">");
        sb.Append($"<input type=\"hidden\" name=\"gdiff\" value=\"{WebUtility.HtmlEncode(s.Difficulty ?? string.Empty)}\">");
        sb.Append($"<input type=\"hidden\" name=\"gjd\" value=\"{WebUtility.HtmlEncode(s.JobDescription ?? string.Empty)}\">");
    }

    private static void AppendMicScript(StringBuilder sb)
    {
        // Browser Web Speech API transcribes YOUR spoken answer into the textbox.
        // No server, no keys, nothing recorded.
        sb.Append("<script>(function(){");
        sb.Append("var btn=document.getElementById('micBtn');");
        sb.Append("if(!btn)return;");
        sb.Append("var status=document.getElementById('micStatus');");
        sb.Append("var box=document.getElementById('ans');");
        sb.Append("var SR=window.SpeechRecognition||window.webkitSpeechRecognition;");
        sb.Append("if(!SR){btn.disabled=true;btn.textContent='\ud83c\udfa4 Speech not supported in this browser';return;}");
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
        sb.Append(".hero{background:linear-gradient(120deg,#7c3aed,#2563eb);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:820px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;letter-spacing:-.3px;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".mode{background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.35);padding:6px 12px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap;}");
        sb.Append(".wrap{max-width:820px;margin:-14px auto 40px;padding:0 24px;}");
        sb.Append(".nav{display:flex;gap:8px;margin:22px 0 8px;}");
        sb.Append(".topics{display:flex;flex-wrap:wrap;gap:8px;margin:22px 0 18px;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#7c3aed;color:#7c3aed;}");
        sb.Append(".chip.active{background:#7c3aed;border-color:#7c3aed;color:#fff;}");
        sb.Append(".genpanel{background:linear-gradient(120deg,#faf5ff,#eff6ff);border:1px solid #ddd6fe;border-radius:16px;padding:18px 18px 16px;margin:0 0 18px;box-shadow:0 1px 3px rgba(15,23,42,.05);}");
        sb.Append(".gp-title{font-size:15.5px;font-weight:800;color:#5b21b6;margin-bottom:12px;}");
        sb.Append(".gp-hint{font-size:12.5px;color:#92400e;background:#fef3c7;border-radius:8px;padding:6px 10px;margin-bottom:12px;}");
        sb.Append(".gp-grid{display:grid;grid-template-columns:1fr 1fr .8fr;gap:10px;}");
        sb.Append("@media(max-width:640px){.gp-grid{grid-template-columns:1fr;}}");
        sb.Append(".genpanel label{display:flex;flex-direction:column;font-size:12.5px;font-weight:700;color:#475569;gap:5px;}");
        sb.Append(".genpanel input,.genpanel select,.genpanel textarea{border:1px solid #cbd5e1;border-radius:10px;padding:9px 11px;font-size:14px;font-family:inherit;font-weight:500;color:#0f172a;background:#fff;}");
        sb.Append(".gp-jd{margin-top:10px;}");
        sb.Append(".gp-jd textarea{resize:vertical;}");
        sb.Append(".gp-btn{margin-top:12px;}");
        sb.Append(".gp-notice{background:#fef3c7;border:1px solid #fde68a;color:#92400e;border-radius:12px;padding:11px 14px;font-size:13.5px;margin:0 0 16px;}");
        sb.Append(".qcard{background:#fff;border-radius:16px;padding:22px;box-shadow:0 1px 3px rgba(15,23,42,.07);}");
        sb.Append(".qmeta{display:flex;gap:8px;margin-bottom:10px;}");
        sb.Append(".tag{border-radius:999px;padding:3px 10px;font-size:11.5px;font-weight:700;}");
        sb.Append(".tag.topic{background:#ede9fe;color:#6d28d9;}");
        sb.Append(".tag.lvl-easy{background:#d1fae5;color:#065f46;}");
        sb.Append(".tag.lvl-medium{background:#fef3c7;color:#92400e;}");
        sb.Append(".tag.lvl-hard{background:#fee2e2;color:#b91c1c;}");
        sb.Append(".qprompt{font-size:19px;font-weight:700;line-height:1.4;margin:6px 0 16px;}");
        sb.Append(".answer{width:100%;border:1px solid #cbd5e1;border-radius:12px;padding:13px 15px;font-size:15px;font-family:inherit;resize:vertical;}");
        sb.Append(".answer:focus{outline:none;border-color:#7c3aed;box-shadow:0 0 0 3px rgba(124,58,237,.15);}");
        sb.Append(".actions{display:flex;gap:10px;margin-top:12px;flex-wrap:wrap;}");
        sb.Append(".btn{border:none;border-radius:12px;padding:12px 18px;font-size:14.5px;font-weight:700;font-family:inherit;cursor:pointer;text-decoration:none;display:inline-block;}");
        sb.Append(".btn-primary{background:#7c3aed;color:#fff;}.btn-primary:hover{background:#6d28d9;}");
        sb.Append(".btn-ghost{background:#f1f5f9;color:#334155;}.btn-ghost:hover{background:#e2e8f0;}");
        sb.Append(".btn-mic{background:#ede9fe;color:#6d28d9;}.btn-mic:hover{background:#ddd6fe;}");
        sb.Append(".btn-mic:disabled{opacity:.6;cursor:not-allowed;}");
        sb.Append(".mic-status{align-self:center;font-size:13px;color:#7c3aed;font-weight:600;}");
        sb.Append(".fb{background:#fff;border-radius:16px;padding:20px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-top:16px;}");
        sb.Append(".fb-head{display:flex;align-items:center;gap:16px;}");
        sb.Append(".score{width:64px;height:64px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:19px;font-weight:800;color:#fff;flex:0 0 auto;}");
        sb.Append(".score-good{background:#10b981;}.score-ok{background:#f59e0b;}.score-low{background:#ef4444;}");
        sb.Append(".fb-comment{font-size:15px;font-weight:500;}");
        sb.Append(".points{margin-top:12px;font-size:14px;color:#334155;line-height:1.5;}");
        sb.Append(".points.miss{color:#7c2d12;}");
        sb.Append(".ainote{margin-top:12px;background:#f5f3ff;border:1px solid #ddd6fe;border-radius:12px;padding:12px 14px;font-size:14px;line-height:1.5;}");
        sb.Append(".model{margin-top:14px;background:#0f172a;color:#e2e8f0;border-radius:12px;padding:16px 20px;}");
        sb.Append(".model summary{color:#fff;font-weight:800;cursor:pointer;font-size:16px;}");
        sb.Append(".model p{margin:12px 0 0;line-height:1.75;font-size:17px;}");
        sb.Append(".simple{margin-top:14px;background:rgba(16,185,129,.14);border-left:4px solid #10b981;border-radius:10px;padding:14px 16px;font-family:Georgia,'Times New Roman',serif;font-size:18px;line-height:1.65;color:#bbf7d0;}");
        sb.Append(".slabel{font-weight:700;color:#6ee7b7;margin-right:4px;font-family:'Inter',sans-serif;}");
        sb.Append(".stext{font-style:italic;}");
        sb.Append(".empty{background:#fff;border-radius:16px;padding:44px 24px;text-align:center;color:#64748b;box-shadow:0 1px 3px rgba(15,23,42,.06);}");
        sb.Append(".empty-emoji{font-size:44px;}.empty h3{margin:10px 0 4px;color:#0f172a;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
