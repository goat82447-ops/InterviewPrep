using System.Text;

namespace InterviewPrep.Web;

/// <summary>Renders the landing page: a polished self-introduction the user can
/// read, rehearse, and copy before an interview. Content is tailored to a
/// full-stack .NET + Azure CI/CD focus.</summary>
internal static class IntroPage
{
    public static string Render()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Self Introduction</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83d\ude4b</span><div>");
        sb.Append("<div class=\"brand-name\">Krishna Kumar Bandoju</div>");
        sb.Append("<div class=\"brand-tag\">Azure DevOps / CI-CD &amp; .NET Engineer \u00b7 ~8 years \u00b7 Hyderabad</div>");
        sb.Append("</div></div>");
        sb.Append("<span class=\"mode\">Interview-ready intro</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip active\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("</div>");

        sb.Append("<p class=\"lead\">Read it a few times, then say it in your own words. " +
                  "Full version for a relaxed opening, short version when the interviewer says " +
                  "&ldquo;tell me about yourself&rdquo; and wants it crisp.</p>");

        // ---- Full version ----
        sb.Append("<section class=\"card\">");
        sb.Append("<div class=\"cardhead\"><span class=\"badge\">Full version \u00b7 ~2 min</span>");
        sb.Append("<button class=\"copy\" type=\"button\" data-copy=\"full\">\ud83d\udccb Copy</button></div>");
        sb.Append("<div class=\"speech\" id=\"full\">");
        sb.Append("<p>Hi, my name is <b>Krishna Kumar Bandoju</b>. I have around <b>8 years</b> of experience in <b>software development, automation, and DevOps engineering</b>.</p>");
        sb.Append("<p>I started my career working on enterprise application development and spent nearly <b>4 years with Ambluoc</b>, where I was involved in software development and automation activities. Currently, I am working with <b>Tata Consultancy Services</b>, supporting <b>Microsoft's Dynamics 365 Omnichannel and ACD</b> platform.</p>");
        sb.Append("<p>In my current role, I am responsible for <b>CI/CD pipeline management, release engineering, and production support</b>. I design and maintain <b>Azure DevOps</b> pipelines, implement automation solutions, and ensure smooth application deployments across environments.</p>");
        sb.Append("<p>I also work closely with <b>global teams</b> to troubleshoot and resolve production issues, including region-specific incidents affecting customers in locations such as <b>Canada</b> and other markets. My responsibilities include <b>root cause analysis, deployment support, monitoring, and improving system reliability</b>.</p>");
        sb.Append("<p>One of my key contributions has been <b>optimising and standardising CI/CD processes</b>, improving deployment efficiency, reducing manual effort, and ensuring stable releases.</p>");
        sb.Append("<p>Overall, my expertise lies in <b>Azure DevOps, CI/CD automation, .NET technologies, production support, release management</b>, and problem-solving in large-scale enterprise environments.</p>");
        sb.Append("</div></section>");

        // ---- Short version ----
        sb.Append("<section class=\"card\">");
        sb.Append("<div class=\"cardhead\"><span class=\"badge short\">Short version \u00b7 ~1 min</span>");
        sb.Append("<button class=\"copy\" type=\"button\" data-copy=\"short\">\ud83d\udccb Copy</button></div>");
        sb.Append("<div class=\"speech\" id=\"short\">");
        sb.Append("<p>Hi, I'm <b>Krishna</b>, and I have around <b>8 years</b> of experience in <b>software development, automation, and DevOps</b>. Currently, I work at <b>TCS for Microsoft's Dynamics 365 Omnichannel</b> platform.</p>");
        sb.Append("<p>My primary responsibilities include managing <b>Azure DevOps CI/CD pipelines</b>, handling <b>production support</b>, resolving critical incidents, and improving deployment automation. I also work on <b>release management, root cause analysis, and reliability improvements</b>.</p>");
        sb.Append("<p>My core strengths are <b>Azure DevOps, CI/CD automation, .NET technologies, and production support</b>.</p>");
        sb.Append("</div></section>");

        // ---- Quick tips ----
        sb.Append("<section class=\"card tips\">");
        sb.Append("<div class=\"tiptitle\">\u2728 Delivery tips</div>");
        sb.Append("<ul>");
        sb.Append("<li>Lead with your <b>name, years, and current project</b> \u2014 then your stack.</li>");
        sb.Append("<li>Say <b>&ldquo;full-stack&rdquo;</b> early so they slot you into full-stack rounds.</li>");
        sb.Append("<li>Mention <b>Azure + CI/CD</b> as a strength, but keep automation brief unless asked.</li>");
        sb.Append("<li>End with what you <b>want next</b> \u2014 it signals direction and confidence.</li>");
        sb.Append("<li>Practice it out loud on the <a href=\"/mock\">Mock interview</a> page.</li>");
        sb.Append("</ul></section>");

        sb.Append("<p class=\"foot\">Know it well enough to say it naturally \u2014 not memorised word for word.</p>");
        AppendCopyScript(sb);
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendCopyScript(StringBuilder sb)
    {
        sb.Append("<script>(function(){");
        sb.Append("document.querySelectorAll('.copy').forEach(function(b){b.addEventListener('click',function(){");
        sb.Append("var el=document.getElementById(b.getAttribute('data-copy'));if(!el)return;");
        sb.Append("var text=el.innerText||el.textContent;");
        sb.Append("navigator.clipboard.writeText(text).then(function(){var o=b.textContent;b.textContent='\u2705 Copied';setTimeout(function(){b.textContent=o;},1500);});");
        sb.Append("});});");
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
        sb.Append(".nav{display:flex;gap:8px;margin:22px 0 16px;flex-wrap:wrap;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".chip.active{background:#0891b2;border-color:#0891b2;color:#fff;}");
        sb.Append(".lead{color:#475569;font-size:14.5px;line-height:1.6;margin:6px 0 18px;}");
        sb.Append(".card{background:#fff;border-radius:16px;padding:22px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-bottom:16px;}");
        sb.Append(".cardhead{display:flex;align-items:center;justify-content:space-between;margin-bottom:12px;gap:12px;}");
        sb.Append(".badge{background:#ecfeff;color:#0e7490;border:1px solid #a5f3fc;font-size:12px;font-weight:700;padding:5px 12px;border-radius:999px;}");
        sb.Append(".badge.short{background:#f0fdf4;color:#15803d;border-color:#bbf7d0;}");
        sb.Append(".copy{background:#f1f5f9;border:1px solid #e2e8f0;border-radius:10px;padding:7px 12px;font-size:13px;font-weight:700;font-family:inherit;color:#334155;cursor:pointer;}");
        sb.Append(".copy:hover{background:#e2e8f0;}");
        sb.Append(".speech p{font-size:15.5px;line-height:1.7;margin:0 0 12px;}");
        sb.Append(".speech p:last-child{margin-bottom:0;}");
        sb.Append(".tips .tiptitle{font-size:15px;font-weight:800;margin-bottom:10px;}");
        sb.Append(".tips ul{margin:0;padding-left:20px;}");
        sb.Append(".tips li{font-size:14px;line-height:1.7;color:#334155;}");
        sb.Append(".tips a{color:#0891b2;font-weight:700;text-decoration:none;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
