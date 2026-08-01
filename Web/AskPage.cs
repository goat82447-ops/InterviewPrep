using System.Net;
using System.Text;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Web;

/// <summary>Renders the "Ask a technical question" study page.</summary>
internal static class AskPage
{
    public static string Render(
        string? question, string? answer, string? source, bool aiEnabled,
        IReadOnlyList<AiProvider> models, string? selectedModel, string? notice = null,
        string? usage = null, string? sessionId = null)
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

        // Quota / key notice banner, shown at the top when the live model could
        // not be used (e.g. daily quota reached) and the study bank answered.
        if (!string.IsNullOrWhiteSpace(notice))
        {
            sb.Append($"<div class=\"notice\">{WebUtility.HtmlEncode(notice)}</div>");
        }

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip active\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/agent\">\ud83e\udd16 Agent mode</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/interview\">\ud83e\udde9 Interview mode</a>");

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
        sb.Append("<button class=\"btn btn-pip\" type=\"button\" id=\"pipBtn\" title=\"Move the answer into a small private floating window only you can see\">\ud83d\udccc Answer in private window</button>");
        sb.Append("<span class=\"mic-status\" id=\"micStatus\"></span>");
        sb.Append("</div>");

        // Cover / decoy document sits right under the buttons, above the answer.
        // The file is chosen in the browser and never uploaded; when "Hide for
        // sharing" is on it fills the whole screen.
        sb.Append("<div class=\"coverrow\">");
        sb.Append("<label class=\"coverlabel\" for=\"coverFile\">\ud83d\udcc4 Cover document (shown on your screen while hidden):</label>");
        sb.Append("<input class=\"coverinput\" type=\"file\" id=\"coverFile\" accept=\"image/*,application/pdf,text/plain,.txt,.pdf,.png,.jpg,.jpeg,.gif,.webp\">");
        sb.Append("</div>");
        sb.Append("</form>");

        // Answer FIRST, right under the button, so it is the very first thing you
        // see (no scrolling). Your question already shows in the text box above, so
        // we don't repeat it here. Memory is still kept for follow-ups.
        var convo = InterviewPrep.Services.StudyAssistant.GetConversation(sessionId);
        if (convo.Count > 0)
        {
            // Find the most recent answer.
            var lastA = -1;
            for (var i = convo.Count - 1; i >= 0; i--)
            {
                if (string.Equals(convo[i].Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    lastA = i;
                    break;
                }
            }

            if (lastA >= 0)
            {
                sb.Append("<section class=\"acard\">");
                if (!string.IsNullOrWhiteSpace(source))
                {
                    sb.Append($"<div class=\"src\">Answer \u00b7 {WebUtility.HtmlEncode(source)}</div>");
                }

                sb.Append($"<div class=\"atext\">{AnswerFormat.ToHtml(convo[lastA].Content)}</div>");
                if (!string.IsNullOrWhiteSpace(usage))
                {
                    sb.Append($"<div class=\"usage\">\ud83d\udcca {WebUtility.HtmlEncode(usage)} \u2014 switch model above if it runs low</div>");
                }

                sb.Append("</section>");
            }

            // Small memory bar UNDER the answer (so it never pushes the answer down).
            sb.Append("<form method=\"post\" action=\"/ask/reset\" class=\"chatbar\">");
            sb.Append("<span class=\"chatlabel\">\ud83d\udcac Remembers your last question</span>");
            sb.Append("<button class=\"btn btn-newtopic\" type=\"submit\" title=\"Forget the last question and start fresh\">\ud83c\udd95 New topic</button>");
            sb.Append("</form>");
        }
        else
        {
            sb.Append("<div class=\"empty\"><div class=\"empty-emoji\">\ud83e\udde0</div><h3>Ask to learn</h3>");
            sb.Append("<p>Type any technical question and get a simple explanation. " +
                      "Ask a follow-up like \u201cexplain that simpler\u201d or \u201cgive an example\u201d \u2014 " +
                      "it remembers your last question.</p></div>");
        }

        // Sharing & phone helpers live BELOW the answer so they never push the
        // answer down the page. QR code is last.
        sb.Append("<div class=\"tools\">");
        // Interview mode / Phone option tips hidden — they were showing on screen.
        // sb.Append("<p class=\"coverhint\"><b>Interview mode:</b> pick your cover document, ask your question, then click <b>Answer in private window</b>. Your answer opens in a small floating window only <b>you</b> see, while this tab shows the cover document. In Teams, share <b>this browser tab/window</b> (not the whole screen) so the interviewer sees only the document. Press <b>Esc</b> or close the floating window to return. The file stays in your browser \u2014 it is never uploaded.</p>");
        // sb.Append("<p class=\"coverhint\"><b>Phone option:</b> open this app on your <b>phone</b> (same Wi-Fi \u2014 the phone address is printed in the terminal when the app starts). Ask and read answers on your phone while you share your PC screen normally, so nothing on the PC ever shows the answer.</p>");

        // Scannable QR code for the phone URL (only when a LAN address was found).
        // Hidden for now — the QR was visible while sharing the screen. Re-enable
        // by uncommenting the block below if you want the phone QR back.
        // if (!string.IsNullOrEmpty(NetworkInfo.QrSvg))
        // {
        //     sb.Append("<div class=\"qrcard\">");
        //     sb.Append("<div class=\"qrtitle\">\ud83d\udcf1 Scan to open on your phone</div>");
        //     sb.Append($"<div class=\"qrbox\">{NetworkInfo.QrSvg}</div>");
        //     sb.Append($"<div class=\"qrurl\">{WebUtility.HtmlEncode(NetworkInfo.PhoneUrl ?? string.Empty)}</div>");
        //     sb.Append("<div class=\"qrnote\">Same Wi-Fi \u00b7 read answers on your phone while you share your PC screen</div>");
        //     sb.Append("</div>");
        // }

        sb.Append("</div>");

        sb.Append("<p class=\"foot\">This is a study helper \u2014 use it to learn and understand, so the knowledge is truly yours.</p>");
        // Full-screen decoy overlay (hidden until the user turns on sharing mode).
        // When no cover document is chosen, it shows a blank Chrome-style new-tab
        // page so the whole screen looks like an ordinary empty browser.
        sb.Append("<div id=\"coverOverlay\" class=\"cover-overlay\">");
        sb.Append("<div id=\"coverDefault\" class=\"ntp\">");
        sb.Append("<div class=\"ntp-top\"><span class=\"lnk\">Gmail</span><span class=\"lnk\">Images</span><span class=\"ntp-apps\">\u2637</span><span class=\"ntp-avatar\">G</span></div>");
        sb.Append("<div class=\"ntp-center\">");
        sb.Append("<div class=\"ntp-logo\"><span style=\"color:#4285f4\">G</span><span style=\"color:#ea4335\">o</span><span style=\"color:#fbbc05\">o</span><span style=\"color:#4285f4\">g</span><span style=\"color:#34a853\">l</span><span style=\"color:#ea4335\">e</span></div>");
        sb.Append("<div class=\"ntp-search\"><span class=\"ntp-ic\">+</span><span class=\"ntp-ph\">Search Google or type a URL</span><span class=\"ntp-ic\">\ud83c\udf99\ufe0f</span><span class=\"ntp-ic\">\ud83d\udcf7</span></div>");
        sb.Append("<div class=\"ntp-tiles\">");
        sb.Append("<div class=\"ntp-tile\"><div class=\"ntp-tico\" style=\"background:#2b3137\">GH</div><div class=\"ntp-tlbl\">GitHub</div></div>");
        sb.Append("<div class=\"ntp-tile\"><div class=\"ntp-tico\" style=\"background:#ff0000\">\u25b6</div><div class=\"ntp-tlbl\">YouTube</div></div>");
        sb.Append("<div class=\"ntp-tile\"><div class=\"ntp-tico\" style=\"background:#ea4335\">M</div><div class=\"ntp-tlbl\">Inbox</div></div>");
        sb.Append("<div class=\"ntp-tile\"><div class=\"ntp-tico\" style=\"background:#10a37f\">AI</div><div class=\"ntp-tlbl\">ChatGPT</div></div>");
        sb.Append("<div class=\"ntp-tile\"><div class=\"ntp-tico\" style=\"background:#5468ff\">R</div><div class=\"ntp-tlbl\">Render</div></div>");
        sb.Append("<div class=\"ntp-tile\"><div class=\"ntp-tico\" style=\"background:#3c4043\">\u22ef</div><div class=\"ntp-tlbl\">Show more</div></div>");
        sb.Append("</div>");
        sb.Append("</div>");
        sb.Append("</div>");
        sb.Append("<div id=\"coverBody\" class=\"cover-body\"></div>");
        sb.Append("</div>");
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

            // Show the last-seen remaining quota so you can pick a model with
            // room left. Only appears after that model has answered at least once.
            var left = InterviewPrep.Services.StudyAssistant.GetLastUsage(m.Id);
            if (m.HasKey && !string.IsNullOrWhiteSpace(left))
            {
                label += $"  \u2014  {left}";
            }

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
        sb.Append("var cd=document.getElementById('coverDefault');");
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
        // Always cover the whole screen when hiding: use the chosen document if
        // there is one, otherwise the blank Chrome-style page.
        sb.Append("if(ov)ov.style.display=on?'block':'none';");
        sb.Append("if(cb)cb.style.display=(on&&hasCover())?'block':'none';");
        sb.Append("if(cd)cd.style.display=(on&&!hasCover())?'flex':'none';");
        sb.Append("}");
        sb.Append("pb.addEventListener('click',function(){setPrivacy(!document.body.classList.contains('privacy'));});");
        sb.Append("document.addEventListener('keydown',function(e){if(e.key==='Escape'&&document.body.classList.contains('privacy')){setPrivacy(false);}});");
        // Double-click anywhere on the decoy to return to your app (the button is
        // covered while hidden). Your question and answer are still there.
        sb.Append("if(ov)ov.addEventListener('dblclick',function(){setPrivacy(false);});");
        sb.Append("window.__ipSetPrivacy=setPrivacy;");
        sb.Append("})();</script>");
        AppendPipScript(sb);
    }

    private static void AppendPipScript(StringBuilder sb)
    {
        // Moves the answer card into a Document Picture-in-Picture window — a
        // small always-on-top window that is SEPARATE from this tab. It also gets
        // its OWN question box + mic, so when the interviewer asks something you
        // can type/speak it and read the answer right here, WITHOUT switching back
        // to the shared tab. When you share only this tab/window in Teams, the
        // floating window is not part of the share. Chromium browsers only.
        sb.Append("<script>(function(){");
        sb.Append("var pb=document.getElementById('pipBtn');");
        sb.Append("if(!pb)return;");
        sb.Append("if(!('documentPictureInPicture' in window)){pb.disabled=true;pb.textContent='\ud83d\udccc Private window (use Chrome/Edge)';pb.title='Your browser does not support the private floating window. Use Chrome or Edge.';return;}");
        sb.Append("var pipWin=null,placeholder=null;");
        sb.Append("function currentModel(){var m=document.querySelector('select.model');return m?m.value:'';}");
        // Ask the server (JSON) and render the answer inside the private window.
        sb.Append("async function ask(q,win){");
        sb.Append("var ansBox=win.document.getElementById('pipAnswer');");
        sb.Append("if(!q||!q.trim()){return;}");
        sb.Append("ansBox.innerHTML='<div class=\"src\">Thinking\u2026</div>';");
        sb.Append("try{var fd=new FormData();fd.append('question',q);fd.append('model',currentModel());");
        sb.Append("var r=await fetch('/ask-json',{method:'POST',body:new URLSearchParams(fd)});");
        sb.Append("var d=await r.json();");
        sb.Append("var note=d.notice?'<div class=\"src\" style=\"background:#fef3c7;border:1px solid #fcd34d;color:#92400e;border-radius:8px;padding:8px 10px;margin-bottom:8px;\">'+d.notice+'</div>':'';");
        sb.Append("var use=d.usage?'<div class=\"src\" style=\"background:#ecfdf5;border:1px solid #a7f3d0;color:#0f766e;border-radius:8px;padding:6px 10px;margin-top:6px;font-weight:700;\">\ud83d\udcca '+d.usage+'</div>':'';");
        sb.Append("ansBox.innerHTML=note+'<section class=\"acard\"><div class=\"src\">Answer \u00b7 '+(d.source||'')+'</div>'+use+'<div class=\"atext\">'+(d.html||'')+'</div></section>';");
        sb.Append("}catch(e){ansBox.innerHTML='<div class=\"src\">Could not get an answer. Try again.</div>';}");
        sb.Append("}");
        // Build the mini ask UI (question box + Ask + mic + answer area) in the PiP window.
        sb.Append("function buildUi(win){");
        sb.Append("var wrap=win.document.createElement('div');");
        sb.Append("var ta=win.document.createElement('textarea');ta.id='pipQ';ta.className='q';ta.rows=2;ta.placeholder='Type or speak the question the interviewer asked\u2026';");
        sb.Append("var row=win.document.createElement('div');row.className='actions';");
        sb.Append("var ask=win.document.createElement('button');ask.className='btn btn-primary';ask.type='button';ask.textContent='Answer';");
        sb.Append("var mic=win.document.createElement('button');mic.className='btn btn-mic';mic.type='button';mic.textContent='\ud83c\udfa4 Speak';");
        sb.Append("row.appendChild(ask);row.appendChild(mic);");
        sb.Append("var ans=win.document.createElement('div');ans.id='pipAnswer';");
        sb.Append("wrap.appendChild(ta);wrap.appendChild(row);wrap.appendChild(ans);");
        sb.Append("win.document.body.appendChild(wrap);");
        sb.Append("ask.addEventListener('click',function(){askNow(ta.value,win);});");
        sb.Append("ta.addEventListener('keydown',function(e){if(e.key==='Enter'&&(e.ctrlKey||e.metaKey)){askNow(ta.value,win);}});");
        // Mic inside the private window (Web Speech API).
        sb.Append("var SR=window.SpeechRecognition||window.webkitSpeechRecognition;");
        sb.Append("if(!SR){mic.disabled=true;mic.textContent='\ud83c\udfa4 n/a';}else{");
        sb.Append("var rec=new SR();rec.lang='en-US';rec.interimResults=true;rec.continuous=false;var listening=false,base='';");
        sb.Append("mic.addEventListener('click',function(){if(listening){rec.stop();return;}base='';ta.value='';try{rec.start();}catch(e){}});");
        sb.Append("rec.onstart=function(){listening=true;mic.textContent='\u23f9 Stop';};");
        sb.Append("rec.onend=function(){listening=false;mic.textContent='\ud83c\udfa4 Speak';};");
        sb.Append("rec.onresult=function(ev){var t='';for(var i=0;i<ev.results.length;i++){t+=ev.results[i][0].transcript;}ta.value=base+t;};");
        sb.Append("}");
        sb.Append("return ans;");
        sb.Append("}");
        sb.Append("function askNow(v,win){ask(v,win);}");
        sb.Append("function restore(){if(placeholder){placeholder.remove();placeholder=null;}pb.classList.remove('active');pb.textContent='\ud83d\udccc Answer in private window';pipWin=null;if(window.__ipSetPrivacy)window.__ipSetPrivacy(false);}");
        sb.Append("pb.addEventListener('click',async function(){");
        sb.Append("if(pipWin){pipWin.close();return;}");
        sb.Append("try{pipWin=await documentPictureInPicture.requestWindow({width:460,height:640});}catch(e){return;}");
        sb.Append("[].forEach.call(document.querySelectorAll('style,link[rel=\"stylesheet\"]'),function(n){pipWin.document.head.appendChild(n.cloneNode(true));});");
        sb.Append("pipWin.document.body.style.margin='0';pipWin.document.body.style.padding='16px';pipWin.document.body.style.background='#f1f5f9';");
        sb.Append("var ansBox=buildUi(pipWin);");
        // If the main tab already has an answer, show it in the private window too.
        sb.Append("var ac=document.querySelector('.acard');if(ac){ansBox.innerHTML=ac.outerHTML;}");
        sb.Append("pb.classList.add('active');pb.textContent='\ud83d\udccc Close private window';");
        sb.Append("if(window.__ipSetPrivacy)window.__ipSetPrivacy(true);");
        sb.Append("pipWin.addEventListener('pagehide',restore);");
        sb.Append("});");
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
        sb.Append(".notice{background:#fef3c7;border:1px solid #fcd34d;color:#92400e;border-radius:12px;padding:12px 16px;font-size:13.5px;font-weight:600;line-height:1.55;margin-top:22px;}");
        sb.Append(".usage{margin-top:6px;font-size:12.5px;font-weight:700;color:#0f766e;background:#ecfdf5;border:1px solid #a7f3d0;border-radius:8px;padding:6px 10px;display:inline-block;}");
        sb.Append(".chatbar{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-top:18px;flex-wrap:wrap;}");
        sb.Append(".chatlabel{font-size:12.5px;font-weight:700;color:#0e7490;background:#ecfeff;border:1px solid #a5f3fc;border-radius:999px;padding:6px 12px;}");
        sb.Append(".btn-newtopic{background:#fff;color:#334155;border:1px solid #cbd5e1;padding:8px 14px;font-size:13px;}.btn-newtopic:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".chat{display:flex;flex-direction:column;gap:12px;margin-top:12px;}");
        sb.Append(".ububble{align-self:flex-end;max-width:85%;background:#0891b2;color:#fff;border-radius:14px 14px 4px 14px;padding:12px 16px;font-size:16px;font-weight:600;line-height:1.55;white-space:pre-wrap;}");
        sb.Append(".abubble{align-self:flex-start;max-width:92%;margin-top:0;border-radius:14px 14px 14px 4px;}");
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
        sb.Append(".btn-pip{background:#ecfeff;color:#0e7490;border:1px solid #a5f3fc;margin-left:8px;}.btn-pip:hover{background:#cffafe;}");
        sb.Append(".btn-pip.active{background:#0e7490;color:#fff;border-color:#0e7490;}");
        sb.Append(".btn-pip:disabled{opacity:.6;cursor:not-allowed;}");
        sb.Append("body.privacy .acard,body.privacy .btn-mic,body.privacy .mic-status{display:none;}");
        sb.Append(".coverrow{display:flex;align-items:center;gap:10px;margin-top:12px;flex-wrap:wrap;}");
        sb.Append(".coverlabel{font-size:13px;font-weight:700;color:#334155;}");
        sb.Append(".coverinput{font-size:13px;font-family:inherit;}");
        sb.Append(".coverhint{font-size:12px;color:#64748b;margin:8px 0 0;}");
        sb.Append(".tools{margin-top:18px;padding-top:16px;border-top:1px solid #e2e8f0;}");
        sb.Append(".qrcard{background:#fff;border:1px solid #e2e8f0;border-radius:14px;padding:16px;margin-top:14px;text-align:center;max-width:240px;box-shadow:0 1px 3px rgba(15,23,42,.06);}");
        sb.Append(".qrtitle{font-size:13px;font-weight:700;color:#0f172a;margin-bottom:8px;}");
        sb.Append(".qrbox{width:170px;height:170px;margin:0 auto;}");
        sb.Append(".qrbox svg{width:100%;height:100%;display:block;}");
        sb.Append(".qrurl{font-size:12px;font-weight:600;color:#0e7490;margin-top:8px;word-break:break-all;}");
        sb.Append(".qrnote{font-size:11px;color:#94a3b8;margin-top:6px;}");
        sb.Append(".cover-overlay{display:none;position:fixed;inset:0;background:#fff;z-index:99999;}");
        sb.Append(".cover-body{width:100%;height:100%;}");
        // Blank Chrome-style new-tab decoy (shown when no cover file is chosen).
        sb.Append(".ntp{display:none;flex-direction:column;width:100%;height:100%;background:#202124;color:#e8eaed;font-family:Arial,sans-serif;}");
        sb.Append(".ntp-top{display:flex;align-items:center;justify-content:flex-end;gap:18px;padding:14px 24px;font-size:13px;}");
        sb.Append(".ntp-top .lnk{color:#e8eaed;}");
        sb.Append(".ntp-apps{font-size:18px;color:#9aa0a6;}");
        sb.Append(".ntp-avatar{width:28px;height:28px;border-radius:50%;background:#e8710a;color:#fff;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:600;}");
        sb.Append(".ntp-center{flex:1;display:flex;flex-direction:column;align-items:center;padding-top:13vh;}");
        sb.Append(".ntp-logo{font-family:'Product Sans',Arial,sans-serif;font-size:70px;font-weight:500;letter-spacing:-2px;margin-bottom:26px;}");
        sb.Append(".ntp-search{display:flex;align-items:center;gap:14px;width:min(560px,90vw);height:48px;padding:0 18px;background:#303134;border:1px solid #5f6368;border-radius:26px;color:#9aa0a6;font-size:16px;}");
        sb.Append(".ntp-ph{flex:1;}");
        sb.Append(".ntp-ic{font-size:18px;opacity:.85;}");
        sb.Append(".ntp-tiles{display:flex;gap:26px;margin-top:34px;flex-wrap:wrap;justify-content:center;max-width:640px;}");
        sb.Append(".ntp-tile{display:flex;flex-direction:column;align-items:center;gap:8px;width:76px;}");
        sb.Append(".ntp-tico{width:48px;height:48px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:17px;font-weight:700;color:#fff;}");
        sb.Append(".ntp-tlbl{font-size:12px;color:#e8eaed;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:76px;}");
        sb.Append(".cover-img{width:100%;height:100%;object-fit:contain;background:#fff;}");
        sb.Append(".cover-frame{width:100%;height:100%;border:none;}");
        sb.Append(".cover-text{margin:0;padding:24px;font-family:Consolas,monospace;font-size:14px;white-space:pre-wrap;overflow:auto;height:100%;background:#fff;color:#0f172a;}");
        sb.Append(".mic-status{margin-left:10px;font-size:13px;color:#0891b2;font-weight:600;}");
        sb.Append(".acard{background:#fff;border-radius:16px;padding:22px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-top:16px;border-left:5px solid #0891b2;}");
        sb.Append(".src{font-size:11.5px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;color:#0891b2;margin-bottom:10px;}");
        sb.Append(".atext{font-size:17.5px;line-height:1.75;color:#0f172a;}");
        AnswerFormat.AppendSayStyles(sb);
        sb.Append(".empty{background:#fff;border-radius:16px;padding:44px 24px;text-align:center;color:#64748b;box-shadow:0 1px 3px rgba(15,23,42,.06);}");
        sb.Append(".empty-emoji{font-size:44px;}.empty h3{margin:10px 0 4px;color:#0f172a;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
