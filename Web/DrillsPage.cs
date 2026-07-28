using System.Net;
using System.Text;
using System.Text.Json;
using InterviewPrep.Data;

namespace InterviewPrep.Web;

/// <summary>Renders the "Rapid drills" page: fast flashcards to make answers
/// automatic. Question on the front, a simple spoken answer on the back. All
/// cards ship to the browser and cycle client-side, so it is instant.</summary>
internal static class DrillsPage
{
    public static string Render(string? topic)
    {
        var pool = string.IsNullOrWhiteSpace(topic)
            ? QuestionBank.Questions
            : QuestionBank.ForTopic(topic);

        if (pool.Count == 0)
        {
            pool = QuestionBank.Questions;
        }

        // Ship a lightweight card list to the browser.
        var cards = pool.Select(q => new
        {
            topic = q.Topic,
            level = q.Level.ToString(),
            prompt = q.Prompt,
            simple = q.SimpleAnswer,
            points = q.KeyPoints,
        });
        var cardsJson = JsonSerializer.Serialize(cards);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Rapid Drills</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\u26a1</span><div>");
        sb.Append("<div class=\"brand-name\">Rapid Drills</div>");
        sb.Append("<div class=\"brand-tag\">Flip-through flashcards \u00b7 say the answer \u00b7 make it automatic</div>");
        sb.Append("</div></div>");
        sb.Append("<span class=\"mode\" id=\"progress\">0 / 0</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip active\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("</div>");

        // Topic chips
        sb.Append("<div class=\"topics\">");
        var anyActive = string.IsNullOrWhiteSpace(topic) ? " active" : string.Empty;
        sb.Append($"<a class=\"chip{anyActive}\" href=\"/drills\">\ud83c\udfb2 All topics</a>");
        foreach (var t in QuestionBank.Topics)
        {
            var active = string.Equals(t, topic, StringComparison.OrdinalIgnoreCase) ? " active" : string.Empty;
            sb.Append($"<a class=\"chip{active}\" href=\"/drills?topic={WebUtility.UrlEncode(t)}\">{WebUtility.HtmlEncode(t)}</a>");
        }

        sb.Append("</div>");

        // Card + controls (populated by JS)
        sb.Append("<section class=\"card\" id=\"card\">");
        sb.Append("<div class=\"cmeta\"><span class=\"tag\" id=\"ctopic\"></span><span class=\"tag lvl\" id=\"clevel\"></span></div>");
        sb.Append("<div class=\"front\" id=\"front\"></div>");
        sb.Append("<div class=\"back\" id=\"back\" style=\"display:none\"></div>");
        sb.Append("<div class=\"controls\">");
        sb.Append("<button class=\"btn btn-primary\" id=\"revealBtn\">Reveal answer</button>");
        sb.Append("<button class=\"btn btn-good\" id=\"gotBtn\" style=\"display:none\">\u2705 Got it</button>");
        sb.Append("<button class=\"btn btn-again\" id=\"againBtn\" style=\"display:none\">\ud83d\udd01 Review again</button>");
        sb.Append("</div>");
        sb.Append("<div class=\"score\" id=\"score\"></div>");
        sb.Append("</section>");

        // Done panel
        sb.Append("<section class=\"done\" id=\"done\" style=\"display:none\">");
        sb.Append("<div class=\"done-emoji\">\ud83c\udf89</div><h3 id=\"doneTitle\">Round complete</h3>");
        sb.Append("<p id=\"doneMsg\"></p>");
        sb.Append("<div class=\"controls\">");
        sb.Append("<button class=\"btn btn-primary\" id=\"reviewBtn\">Review the ones I missed</button>");
        sb.Append("<button class=\"btn btn-ghost\" id=\"restartBtn\">Start over</button>");
        sb.Append("</div></section>");

        sb.Append("<p class=\"foot\">Say each answer out loud before you flip \u2014 repetition is what makes it automatic.</p>");
        AppendScript(sb, cardsJson);
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendScript(StringBuilder sb, string cardsJson)
    {
        sb.Append("<script>");
        sb.Append("var ALL=").Append(cardsJson).Append(";");
        sb.Append("var deck=ALL.slice();var i=0;var got=0;var missed=[];");
        sb.Append("var elCard=document.getElementById('card');");
        sb.Append("var elDone=document.getElementById('done');");
        sb.Append("var front=document.getElementById('front');var back=document.getElementById('back');");
        sb.Append("var ctopic=document.getElementById('ctopic');var clevel=document.getElementById('clevel');");
        sb.Append("var revealBtn=document.getElementById('revealBtn');var gotBtn=document.getElementById('gotBtn');var againBtn=document.getElementById('againBtn');");
        sb.Append("var prog=document.getElementById('progress');var scoreEl=document.getElementById('score');");
        sb.Append("function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}");
        sb.Append("function show(){if(i>=deck.length){finish();return;}var c=deck[i];");
        sb.Append("ctopic.textContent=c.topic;clevel.textContent=c.level;clevel.className='tag lvl '+c.level.toLowerCase();");
        sb.Append("front.innerHTML=esc(c.prompt);");
        sb.Append("var pts=(c.points&&c.points.length)?'<div class=\\'pts\\'><b>Key points:</b> '+c.points.map(esc).join(', ')+'</div>':'';");
        sb.Append("back.innerHTML='<div class=\\'say\\'>\ud83d\udde3 '+esc(c.simple)+'</div>'+pts;");
        sb.Append("back.style.display='none';revealBtn.style.display='';gotBtn.style.display='none';againBtn.style.display='none';");
        sb.Append("prog.textContent=(i+1)+' / '+deck.length;scoreEl.textContent='Got it: '+got;}");
        sb.Append("revealBtn.addEventListener('click',function(){back.style.display='';revealBtn.style.display='none';gotBtn.style.display='';againBtn.style.display='';});");
        sb.Append("gotBtn.addEventListener('click',function(){got++;i++;show();});");
        sb.Append("againBtn.addEventListener('click',function(){missed.push(deck[i]);i++;show();});");
        sb.Append("function finish(){elCard.style.display='none';elDone.style.display='';");
        sb.Append("document.getElementById('doneMsg').textContent='You said \"got it\" to '+got+' of '+deck.length+' cards. Missed: '+missed.length+'.';");
        sb.Append("document.getElementById('reviewBtn').style.display=missed.length?'':'none';}");
        sb.Append("document.getElementById('reviewBtn').addEventListener('click',function(){deck=missed.slice();missed=[];got=0;i=0;elDone.style.display='none';elCard.style.display='';show();});");
        sb.Append("document.getElementById('restartBtn').addEventListener('click',function(){deck=ALL.slice();missed=[];got=0;i=0;elDone.style.display='none';elCard.style.display='';show();});");
        sb.Append("show();");
        sb.Append("</script>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#0d9488,#0891b2);color:#fff;padding:26px 24px;}");
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
        sb.Append(".chip:hover{border-color:#0d9488;color:#0d9488;}");
        sb.Append(".chip.active{background:#0d9488;border-color:#0d9488;color:#fff;}");
        sb.Append(".card{background:#fff;border-radius:16px;padding:26px;box-shadow:0 1px 3px rgba(15,23,42,.07);min-height:220px;}");
        sb.Append(".cmeta{display:flex;gap:8px;margin-bottom:14px;}");
        sb.Append(".tag{border-radius:999px;padding:3px 10px;font-size:11.5px;font-weight:700;background:#ccfbf1;color:#0f766e;}");
        sb.Append(".tag.lvl.Easy{background:#d1fae5;color:#065f46;}.tag.lvl.Medium{background:#fef3c7;color:#92400e;}.tag.lvl.Hard{background:#fee2e2;color:#b91c1c;}");
        sb.Append(".front{font-size:21px;font-weight:700;line-height:1.45;}");
        sb.Append(".back{margin-top:18px;border-top:1px dashed #cbd5e1;padding-top:16px;}");
        sb.Append(".say{font-size:17px;line-height:1.6;color:#0f766e;font-weight:600;font-family:Georgia,'Times New Roman',serif;font-style:italic;background:#ecfdf5;border-left:4px solid #10b981;border-radius:10px;padding:12px 14px;}");
        sb.Append(".pts{margin-top:12px;font-size:14px;color:#475569;line-height:1.5;}");
        sb.Append(".controls{display:flex;gap:10px;margin-top:20px;flex-wrap:wrap;}");
        sb.Append(".btn{border:none;border-radius:12px;padding:12px 18px;font-size:14.5px;font-weight:700;font-family:inherit;cursor:pointer;}");
        sb.Append(".btn-primary{background:#0d9488;color:#fff;}.btn-primary:hover{background:#0f766e;}");
        sb.Append(".btn-good{background:#10b981;color:#fff;}.btn-good:hover{background:#059669;}");
        sb.Append(".btn-again{background:#f59e0b;color:#fff;}.btn-again:hover{background:#d97706;}");
        sb.Append(".btn-ghost{background:#f1f5f9;color:#334155;}.btn-ghost:hover{background:#e2e8f0;}");
        sb.Append(".score{margin-top:14px;font-size:13px;color:#64748b;font-weight:600;}");
        sb.Append(".done{background:#fff;border-radius:16px;padding:40px 24px;text-align:center;box-shadow:0 1px 3px rgba(15,23,42,.06);}");
        sb.Append(".done-emoji{font-size:44px;}.done h3{margin:8px 0 4px;}.done p{color:#64748b;}");
        sb.Append(".done .controls{justify-content:center;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
