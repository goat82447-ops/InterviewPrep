using System.Net;
using System.Text;
using InterviewPrep.Data;

namespace InterviewPrep.Web;

/// <summary>Renders a focused multi-day study plan built from the topic bank,
/// with direct links into drills, practice, and mock interview for each day.</summary>
internal static class StudyPlanPage
{
    public static string Render()
    {
        var topics = QuestionBank.Topics.ToList();

        // Group topics into days of two, so ~8 topics -> a 4-day plan (3-5 range).
        var days = new List<List<string>>();
        for (var i = 0; i < topics.Count; i += 2)
        {
            days.Add(topics.Skip(i).Take(2).ToList());
        }

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Study Plan</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");
        WebChrome.Append(sb);

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83d\uddd3\ufe0f</span><div>");
        sb.Append("<div class=\"brand-name\">Study Plan</div>");
        sb.Append($"<div class=\"brand-tag\">A focused {days.Count}-day plan \u00b7 a little each day \u00b7 walk in genuinely ready</div>");
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
        sb.Append("<a class=\"chip active\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        sb.Append("<div class=\"intro\">Each day takes about 45\u201360 minutes. Do the four steps in order: " +
                  "warm up with drills, learn the strong answers, practice out loud, then a mock interview to pull it together.</div>");

        var dayNum = 1;
        foreach (var day in days)
        {
            var label = string.Join(" & ", day);
            var firstTopic = day[0];

            sb.Append("<section class=\"day\">");
            sb.Append($"<div class=\"day-head\"><span class=\"daynum\">Day {dayNum}</span><span class=\"daytopics\">{WebUtility.HtmlEncode(label)}</span></div>");

            sb.Append("<ol class=\"steps\">");
            foreach (var t in day)
            {
                var enc = WebUtility.UrlEncode(t);
                var name = WebUtility.HtmlEncode(t);
                sb.Append("<li><div class=\"step\">");
                sb.Append($"<span class=\"stitle\">\u26a1 Warm-up drills \u2014 {name}</span>");
                sb.Append($"<a class=\"go\" href=\"/drills?topic={enc}\">Open drills \u2192</a>");
                sb.Append("</div><div class=\"sdesc\">Flip through the cards and say each answer out loud until it feels automatic.</div></li>");

                sb.Append("<li><div class=\"step\">");
                sb.Append($"<span class=\"stitle\">\ud83c\udf93 Practice out loud \u2014 {name}</span>");
                sb.Append($"<a class=\"go\" href=\"/practice?topic={enc}\">Open practice \u2192</a>");
                sb.Append("</div><div class=\"sdesc\">Answer with the mic in your own words, then compare with the strong model answer.</div></li>");
            }

            sb.Append("<li><div class=\"step\">");
            sb.Append($"<span class=\"stitle\">\ud83c\udf99\ufe0f Mock interview \u2014 {WebUtility.HtmlEncode(firstTopic)}</span>");
            sb.Append($"<a class=\"go\" href=\"/mock?topic={WebUtility.UrlEncode(firstTopic)}\">Start mock \u2192</a>");
            sb.Append("</div><div class=\"sdesc\">Answer, take the follow-up questions, and handle the deeper probing like a real interview.</div></li>");
            sb.Append("</ol>");
            sb.Append("</section>");
            dayNum++;
        }

        // Final day: full mixed mock + weak spots.
        sb.Append("<section class=\"day final\">");
        sb.Append($"<div class=\"day-head\"><span class=\"daynum\">Day {dayNum}</span><span class=\"daytopics\">Full dress rehearsal</span></div>");
        sb.Append("<ol class=\"steps\">");
        sb.Append("<li><div class=\"step\"><span class=\"stitle\">\u26a1 All-topics drill round</span>");
        sb.Append("<a class=\"go\" href=\"/drills\">Open drills \u2192</a></div>");
        sb.Append("<div class=\"sdesc\">Do one full pass across every topic and mark the ones you miss to review again.</div></li>");
        sb.Append("<li><div class=\"step\"><span class=\"stitle\">\ud83c\udf99\ufe0f Mixed mock interview</span>");
        sb.Append("<a class=\"go\" href=\"/mock\">Start mock \u2192</a></div>");
        sb.Append("<div class=\"sdesc\">Any topic, back-to-back questions and follow-ups \u2014 simulate the real thing end to end.</div></li>");
        sb.Append("<li><div class=\"step\"><span class=\"stitle\">\ud83d\udca1 Clear up anything shaky</span>");
        sb.Append("<a class=\"go\" href=\"/ask\">Ask &amp; Learn \u2192</a></div>");
        sb.Append("<div class=\"sdesc\">Ask about anything that still feels unclear so nothing surprises you.</div></li>");
        sb.Append("</ol>");
        sb.Append("</section>");

        sb.Append("<p class=\"foot\">Consistency beats cramming \u2014 a focused hour a day makes the answers truly yours.</p>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#4338ca,#7c3aed);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:820px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;letter-spacing:-.3px;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".wrap{max-width:820px;margin:-14px auto 40px;padding:0 24px;}");
        sb.Append(".nav{display:flex;gap:8px;margin:22px 0 16px;flex-wrap:wrap;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#7c3aed;color:#7c3aed;}");
        sb.Append(".chip.active{background:#7c3aed;border-color:#7c3aed;color:#fff;}");
        sb.Append(".intro{background:#eef2ff;border:1px solid #e0e7ff;border-radius:14px;padding:14px 16px;font-size:14px;line-height:1.6;color:#3730a3;margin-bottom:18px;}");
        sb.Append(".day{background:#fff;border-radius:16px;padding:20px 22px;box-shadow:0 1px 3px rgba(15,23,42,.07);margin-bottom:16px;}");
        sb.Append(".day.final{border:1px solid #ddd6fe;}");
        sb.Append(".day-head{display:flex;align-items:center;gap:12px;margin-bottom:12px;}");
        sb.Append(".daynum{background:#7c3aed;color:#fff;border-radius:999px;padding:5px 14px;font-size:13px;font-weight:800;}");
        sb.Append(".daytopics{font-size:17px;font-weight:700;}");
        sb.Append(".steps{margin:0;padding-left:0;list-style:none;}");
        sb.Append(".steps li{border-top:1px solid #f1f5f9;padding:12px 0;}");
        sb.Append(".steps li:first-child{border-top:none;}");
        sb.Append(".step{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;}");
        sb.Append(".stitle{font-size:15px;font-weight:600;}");
        sb.Append(".go{background:#f5f3ff;color:#6d28d9;border-radius:10px;padding:7px 12px;font-size:13px;font-weight:700;text-decoration:none;white-space:nowrap;}");
        sb.Append(".go:hover{background:#ede9fe;}");
        sb.Append(".sdesc{font-size:13.5px;color:#64748b;margin-top:5px;line-height:1.5;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:22px;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }
}
