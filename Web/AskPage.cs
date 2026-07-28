using System.Net;
using System.Text;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Web;

/// <summary>Renders the "Ask a technical question" study page.</summary>
internal static class AskPage
{
    public static string Render(
        string? question, string? answer, string? source, bool aiEnabled,
        IReadOnlyList<AiProvider> models, string? selectedModel)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Ask & Learn</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83d\udca1</span><div>");
        sb.Append("<div class=\"brand-name\">Ask &amp; Learn</div>");
        sb.Append("<div class=\"brand-tag\">Ask any technical question \u00b7 get a simple, clear explanation to study</div>");
        sb.Append("</div></div>");
        sb.Append($"<span class=\"mode\">{(aiEnabled ? "AI: on" : "AI: off (study bank)")}</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip active\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("</div>");

        sb.Append("<form method=\"post\" action=\"/ask\">");
        var prior = question ?? string.Empty;
        sb.Append($"<textarea class=\"q\" name=\"question\" id=\"q\" rows=\"3\" placeholder=\"e.g. What is the difference between an abstract class and an interface in C#?\">{WebUtility.HtmlEncode(prior)}</textarea>");
        AppendModelPicker(sb, models, selectedModel);
        sb.Append("<div class=\"actions\">");
        sb.Append("<button class=\"btn btn-primary\" type=\"submit\">Explain it to me</button>");
        sb.Append("<button class=\"btn btn-mic\" type=\"button\" id=\"micBtn\">\ud83c\udfa4 Speak your question</button>");
        sb.Append("<button class=\"btn btn-privacy\" type=\"button\" id=\"privacyBtn\" title=\"Hide the answer and mic while sharing your screen\">\ud83d\ude48 Hide for sharing</button>");
        sb.Append("<span class=\"mic-status\" id=\"micStatus\"></span>");
        sb.Append("</div>");
        sb.Append("</form>");

        // Cover / decoy document: chosen in the browser (never uploaded). When
        // "Hide for sharing" is on, this file fills the whole screen so a screen
        // share shows only this document. Press Esc to return to the real app.
        sb.Append("<div class=\"coverrow\">");
        sb.Append("<label class=\"coverlabel\" for=\"coverFile\">\ud83d\udcc4 Cover document (shown on your screen while hidden):</label>");
        sb.Append("<input class=\"coverinput\" type=\"file\" id=\"coverFile\" accept=\"image/*,application/pdf,text/plain,.txt,.pdf,.png,.jpg,.jpeg,.gif,.webp\">");
        sb.Append("</div>");
        sb.Append("<p class=\"coverhint\">Pick a file, then click <b>Hide for sharing</b> to cover your screen with it. Press <b>Esc</b> to come back. The file stays in your browser \u2014 it is never uploaded.</p>");

        if (!string.IsNullOrWhiteSpace(answer))
        {
            sb.Append("<section class=\"acard\">");
            sb.Append($"<div class=\"src\">Answer \u00b7 {WebUtility.HtmlEncode(source ?? string.Empty)}</div>");
            // Preserve line breaks and style the "Say it simply" line distinctly.
            sb.Append($"<div class=\"atext\">{AnswerFormat.ToHtml(answer)}</div>");
            sb.Append("</section>");
        }
        else
        {
            sb.Append("<div class=\"empty\"><div class=\"empty-emoji\">\ud83e\udde0</div><h3>Ask to learn</h3>");
            sb.Append("<p>Type any technical question and get a simple explanation. " +
                      "Use it to study and understand \u2014 then practice saying it in your own words on the Practice page.</p></div>");
        }

        sb.Append("<p class=\"foot\">This is a study helper \u2014 use it to learn and understand, so the knowledge is truly yours.</p>");
        // Full-screen decoy overlay (hidden until the user turns on sharing mode).
        sb.Append("<div id=\"coverOverlay\" class=\"cover-overlay\"><div id=\"coverBody\" class=\"cover-body\"></div></div>");
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
        sb.Append("<span class=\"mlabel\">\ud83e\udde0 Answer with</span>");
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
    {        // Uses the browser's built-in Web Speech API to transcribe YOUR spoken
        // question into the textbox. No server, no keys, nothing recorded.
        sb.Append("<script>(function(){");
        sb.Append("var btn=document.getElementById('micBtn');");
        sb.Append("var status=document.getElementById('micStatus');");
        sb.Append("var box=document.getElementById('q');");
        sb.Append("var SR=window.SpeechRecognition||window.webkitSpeechRecognition;");
        sb.Append("if(!SR){if(btn){btn.disabled=true;btn.textContent='\ud83c\udfa4 Speech not supported in this browser';}return;}");
        sb.Append("var rec=new SR();rec.lang='en-US';rec.interimResults=true;rec.continuous=false;");
        sb.Append("var listening=false;var base='';");
        sb.Append("btn.addEventListener('click',function(){if(listening){rec.stop();return;}base='';box.value='';var ac=document.querySelector('.acard');if(ac)ac.remove();try{rec.start();}catch(e){}});");
        sb.Append("rec.onstart=function(){listening=true;btn.textContent='\u23f9 Stop listening';status.textContent='Listening\u2026 speak your question';};");
        sb.Append("rec.onerror=function(e){status.textContent='Mic error: '+e.error;};");
        sb.Append("rec.onend=function(){listening=false;btn.textContent='\ud83c\udfa4 Speak your question';if(!status.textContent.startsWith('Mic error'))status.textContent='';};");
        sb.Append("rec.onresult=function(ev){var t='';for(var i=0;i<ev.results.length;i++){t+=ev.results[i][0].transcript;}box.value=base+t;};");
        sb.Append("})();</script>");
        AppendPrivacyScript(sb);
    }

    private static void AppendPrivacyScript(StringBuilder sb)
    {
        // Client-side only. Turning on "Hide for sharing" covers the whole screen
        // with the chosen document (or, if none chosen, just hides the answer and
        // mic). Press Esc to return. Nothing is sent to the server.
        sb.Append("<script>(function(){");
        sb.Append("var pb=document.getElementById('privacyBtn');");
        sb.Append("if(!pb)return;");
        sb.Append("var fi=document.getElementById('coverFile');");
        sb.Append("var ov=document.getElementById('coverOverlay');");
        sb.Append("var cb=document.getElementById('coverBody');");
        sb.Append("var url=null;");
        sb.Append("function clearCover(){if(url){URL.revokeObjectURL(url);url=null;}if(cb)cb.innerHTML='';}");
        sb.Append("function hasCover(){return cb&&cb.children.length>0;}");
        sb.Append("if(fi){fi.addEventListener('change',function(){");
        sb.Append("clearCover();");
        sb.Append("var f=fi.files&&fi.files[0];if(!f)return;");
        sb.Append("var t=f.type||'';");
        sb.Append("if(t.indexOf('image')===0){url=URL.createObjectURL(f);var im=document.createElement('img');im.className='cover-img';im.src=url;cb.appendChild(im);}");
        sb.Append("else if(t.indexOf('pdf')>-1||/\\.pdf$/i.test(f.name)){url=URL.createObjectURL(f);var fr=document.createElement('iframe');fr.className='cover-frame';fr.src=url;cb.appendChild(fr);}");
        sb.Append("else{var r=new FileReader();r.onload=function(){var pre=document.createElement('pre');pre.className='cover-text';pre.textContent=String(r.result);cb.innerHTML='';cb.appendChild(pre);};r.readAsText(f);}");
        sb.Append("});}");
        sb.Append("function setPrivacy(on){");
        sb.Append("document.body.classList.toggle('privacy',on);");
        sb.Append("pb.classList.toggle('active',on);");
        sb.Append("pb.textContent=on?'\ud83d\udc41\ufe0f Show again':'\ud83d\ude48 Hide for sharing';");
        sb.Append("if(ov)ov.style.display=(on&&hasCover())?'block':'none';");
        sb.Append("}");
        sb.Append("pb.addEventListener('click',function(){setPrivacy(!document.body.classList.contains('privacy'));});");
        sb.Append("document.addEventListener('keydown',function(e){if(e.key==='Escape'&&document.body.classList.contains('privacy')){setPrivacy(false);}});");
        sb.Append("})();</script>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#0891b2,#2563eb);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:820px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;letter-spacing:-.3px;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".mode{background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.35);padding:6px 12px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap;}");
        sb.Append(".wrap{max-width:820px;margin:-14px auto 40px;padding:0 24px;}");
        sb.Append(".nav{display:flex;gap:8px;margin:22px 0 16px;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".chip.active{background:#0891b2;border-color:#0891b2;color:#fff;}");
        sb.Append(".q{width:100%;border:1px solid #cbd5e1;border-radius:12px;padding:13px 15px;font-size:15px;font-family:inherit;resize:vertical;}");
        sb.Append(".q:focus{outline:none;border-color:#0891b2;box-shadow:0 0 0 3px rgba(8,145,178,.15);}");
        sb.Append(".actions{margin-top:12px;}");
        sb.Append(".modelrow{display:flex;align-items:center;gap:10px;margin-top:12px;flex-wrap:wrap;}");
        sb.Append(".mlabel{font-size:13px;font-weight:700;color:#334155;}");
        sb.Append(".model{border:1px solid #cbd5e1;border-radius:10px;padding:9px 12px;font-size:14px;font-family:inherit;font-weight:600;color:#0f172a;background:#fff;cursor:pointer;}");
        sb.Append(".model:focus{outline:none;border-color:#0891b2;box-shadow:0 0 0 3px rgba(8,145,178,.15);}");
        sb.Append(".btn{border:none;border-radius:12px;padding:12px 18px;font-size:14.5px;font-weight:700;font-family:inherit;cursor:pointer;}");
        sb.Append(".btn-primary{background:#0891b2;color:#fff;}.btn-primary:hover{background:#0e7490;}");
        sb.Append(".btn-mic{background:#f1f5f9;color:#334155;margin-left:8px;}.btn-mic:hover{background:#e2e8f0;}");
        sb.Append(".btn-mic:disabled{opacity:.6;cursor:not-allowed;}");
        sb.Append(".btn-privacy{background:#f1f5f9;color:#334155;margin-left:8px;}.btn-privacy:hover{background:#e2e8f0;}");
        sb.Append(".btn-privacy.active{background:#0f172a;color:#fff;}");
        sb.Append("body.privacy .acard,body.privacy .btn-mic,body.privacy .mic-status{display:none;}");
        sb.Append(".coverrow{display:flex;align-items:center;gap:10px;margin-top:12px;flex-wrap:wrap;}");
        sb.Append(".coverlabel{font-size:13px;font-weight:700;color:#334155;}");
        sb.Append(".coverinput{font-size:13px;font-family:inherit;}");
        sb.Append(".coverhint{font-size:12px;color:#64748b;margin:8px 0 0;}");
        sb.Append(".cover-overlay{display:none;position:fixed;inset:0;background:#fff;z-index:99999;}");
        sb.Append(".cover-body{width:100%;height:100%;}");
        sb.Append(".cover-img{width:100%;height:100%;object-fit:contain;background:#fff;}");
        sb.Append(".cover-frame{width:100%;height:100%;border:none;}");
        sb.Append(".cover-text{margin:0;padding:24px;font-family:Consolas,monospace;font-size:14px;white-space:pre-wrap;overflow:auto;height:100%;background:#fff;color:#0f172a;}");
        sb.Append(".mic-status{margin-left:10px;font-size:13px;color:#0891b2;font-weight:600;}");
        sb.Append(".acard{background:#fff;border-radius:16px;padding:20px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-top:16px;}");
        sb.Append(".src{font-size:11.5px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;color:#0891b2;margin-bottom:10px;}");
        sb.Append(".atext{font-size:15.5px;line-height:1.65;}");
        AnswerFormat.AppendSayStyles(sb);
        sb.Append(".empty{background:#fff;border-radius:16px;padding:44px 24px;text-align:center;color:#64748b;box-shadow:0 1px 3px rgba(15,23,42,.06);}");
        sb.Append(".empty-emoji{font-size:44px;}.empty h3{margin:10px 0 4px;color:#0f172a;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
