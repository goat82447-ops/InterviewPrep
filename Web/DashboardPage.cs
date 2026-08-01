using System.Text;

namespace InterviewPrep.Web;

/// <summary>Renders the Progress dashboard: an all-client-side view that reads
/// the practice history saved in the browser (the Live interview 'liveLog' and
/// the Interview-mode 'interviewLog') and charts score-over-time, best/average,
/// a simple streak, and per-round strengths so you can see real improvement.</summary>
internal static class DashboardPage
{
    public static string Render()
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Progress dashboard</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");
        WebChrome.Append(sb);

        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83d\udcc8</span><div>");
        sb.Append("<div class=\"brand-name\">Progress dashboard</div>");
        sb.Append("<div class=\"brand-tag\">Your practice history \u00b7 scores over time \u00b7 best &amp; average \u00b7 where you are improving</div>");
        sb.Append("</div></div></div></header>");

        sb.Append("<main class=\"wrap\">");

        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/interview\">\ud83e\udde9 Interview mode</a>");
        sb.Append("<a class=\"chip active\" href=\"/dashboard\">\ud83d\udcc8 Progress</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        sb.Append("<div id=\"empty\" class=\"card\" hidden>");
        sb.Append("<div class=\"card-title\">No practice yet</div>");
        sb.Append("<div class=\"hint\">Run a <a href=\"/live\">Live interview</a> or an <a href=\"/interview\">Interview mode</a> session and your scores will appear here.</div>");
        sb.Append("</div>");

        sb.Append("<div id=\"content\" hidden>");
        sb.Append("<div class=\"kpis\">");
        AppendKpi(sb, "kSessions", "Sessions");
        AppendKpi(sb, "kAvg", "Avg (last 5)");
        AppendKpi(sb, "kBest", "Best score");
        AppendKpi(sb, "kStreak", "Improving streak");
        sb.Append("</div>");

        sb.Append("<div class=\"card\"><div class=\"card-title\">Score over time</div>");
        sb.Append("<div class=\"hint\">Every practice score, oldest to newest.</div>");
        sb.Append("<div id=\"chart\" class=\"chart\"></div></div>");

        sb.Append("<div class=\"card\"><div class=\"card-title\">Interview-mode rounds</div>");
        sb.Append("<div class=\"hint\">Average per round across your full interviews.</div>");
        sb.Append("<div id=\"rounds\" class=\"rounds\"></div></div>");

        sb.Append("<div class=\"card\"><div class=\"card-title\">Recent sessions</div>");
        sb.Append("<div id=\"recent\" class=\"recent\"></div></div>");
        sb.Append("</div>"); // content

        sb.Append("</main>");
        AppendScript(sb);
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendKpi(StringBuilder sb, string id, string label)
    {
        sb.Append("<div class=\"kpi\"><div class=\"kpi-val\" id=\"").Append(id).Append("\">\u2013</div>");
        sb.Append("<div class=\"kpi-lbl\">").Append(label).Append("</div></div>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}body{margin:0;font-family:Inter,system-ui,sans-serif;background:#f1f5f9;color:#0f172a;}");
        sb.Append(".hero{background:linear-gradient(120deg,#7c3aed,#0891b2);color:#fff;padding:22px 0;}");
        sb.Append(".hero-inner{max-width:960px;margin:0 auto;padding:0 20px;display:flex;justify-content:space-between;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;}.logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;}.brand-tag{font-size:13px;opacity:.9;}");
        sb.Append(".wrap{max-width:960px;margin:0 auto;padding:22px 20px 60px;}");
        sb.Append(".nav{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:18px;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip.active{background:#7c3aed;color:#fff;border-color:#7c3aed;}");
        sb.Append(".card{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:20px;margin-bottom:16px;box-shadow:0 1px 2px rgba(0,0,0,.04);}");
        sb.Append(".card-title{font-size:16px;font-weight:700;margin-bottom:4px;}");
        sb.Append(".hint{font-size:13px;color:#64748b;margin-bottom:12px;}");
        sb.Append(".kpis{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:16px;}");
        sb.Append(".kpi{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:16px;text-align:center;}");
        sb.Append(".kpi-val{font-size:26px;font-weight:800;color:#7c3aed;}.kpi-lbl{font-size:12px;color:#64748b;margin-top:4px;}");
        sb.Append(".chart{display:flex;align-items:flex-end;gap:5px;height:160px;padding-top:8px;overflow-x:auto;}");
        sb.Append(".cbar{flex:0 0 16px;min-width:16px;background:linear-gradient(180deg,#7c3aed,#0891b2);border-radius:5px 5px 0 0;position:relative;}");
        sb.Append(".cbar:hover::after{content:attr(data-v);position:absolute;top:-20px;left:50%;transform:translateX(-50%);background:#0f172a;color:#fff;font-size:11px;padding:2px 6px;border-radius:6px;white-space:nowrap;}");
        sb.Append(".rounds{display:flex;flex-direction:column;gap:10px;}");
        sb.Append(".rrow{display:flex;align-items:center;gap:12px;font-size:14px;}.rrow .rn{flex:0 0 150px;font-weight:600;}");
        sb.Append(".rbar{flex:1;height:10px;background:#e2e8f0;border-radius:999px;overflow:hidden;}");
        sb.Append(".rbar span{display:block;height:100%;background:linear-gradient(90deg,#7c3aed,#0891b2);}");
        sb.Append(".recent .rec{display:flex;justify-content:space-between;align-items:center;padding:10px 0;border-bottom:1px solid #eef2f7;font-size:14px;}");
        sb.Append(".rec .badge{font-weight:700;border-radius:8px;padding:3px 9px;color:#fff;}");
        sb.Append("@media(max-width:640px){.kpis{grid-template-columns:repeat(2,1fr);}.hero-inner{flex-direction:column;align-items:flex-start;}}");
        sb.Append("</style>");
    }

    private static void AppendScript(StringBuilder sb)
    {
        sb.Append(@"<script>
(function(){
  function get(k){ try{ return JSON.parse(localStorage.getItem(k)||'[]')||[]; }catch(e){ return []; } }
  var live=get('liveLog');       // [{s,t,vd,d,dur,f}]
  var inter=get('interviewLog'); // [{o,rounds:[{name,avg}],stack,d}]

  // Unified list of scored sessions, oldest to newest.
  var pts=[];
  live.forEach(function(e){ pts.push({score:(e.s|0), when:e.d||'', kind:'Live', label:(e.t||'General')}); });
  inter.forEach(function(e){ pts.push({score:(e.o|0), when:e.d||'', kind:'Interview', label:(e.stack||'Full interview')}); });
  pts.sort(function(a,b){ return (a.when||'').localeCompare(b.when||''); });

  if(!pts.length){ document.getElementById('empty').hidden=false; return; }
  document.getElementById('content').hidden=false;

  var scores=pts.map(function(p){return p.score;});
  var last5=scores.slice(-5);
  var avg=Math.round(last5.reduce(function(s,x){return s+x;},0)/last5.length);
  var best=Math.max.apply(null,scores);

  // Improving streak: how many latest sessions each beat the one before.
  var streak=0;
  for(var i=scores.length-1;i>0;i--){ if(scores[i]>=scores[i-1]){ streak++; } else { break; } }

  document.getElementById('kSessions').textContent=scores.length;
  document.getElementById('kAvg').textContent=avg;
  document.getElementById('kBest').textContent=best;
  document.getElementById('kStreak').textContent=streak;

  function color(s){ return s>=75?'#059669':(s>=55?'#d97706':'#dc2626'); }

  // Bar chart of all scores.
  var chart=document.getElementById('chart');
  pts.forEach(function(p){
    var b=document.createElement('div'); b.className='cbar';
    b.style.height=Math.max(4,p.score)+'%';
    b.style.background=color(p.score);
    b.setAttribute('data-v',p.kind+': '+p.score);
    chart.appendChild(b);
  });

  // Per-round averages across interview-mode sessions.
  var roundsBox=document.getElementById('rounds');
  var agg={}, order=[];
  inter.forEach(function(e){ (e.rounds||[]).forEach(function(r){
    if(!agg[r.name]){ agg[r.name]={sum:0,n:0}; order.push(r.name); }
    agg[r.name].sum+=(r.avg|0); agg[r.name].n++;
  }); });
  if(!order.length){ roundsBox.innerHTML='<div class=\""hint\"">Run an Interview-mode session to see per-round averages.</div>'; }
  else {
    order.forEach(function(name){
      var a=agg[name], v=Math.round(a.sum/Math.max(1,a.n));
      var row=document.createElement('div'); row.className='rrow';
      row.innerHTML='<span class=\""rn\"">'+esc(name)+'</span>'+
        '<span class=\""rbar\""><span style=\""width:'+v+'%\""></span></span>'+
        '<b>'+v+'</b>';
      roundsBox.appendChild(row);
    });
  }

  // Recent sessions (latest 10 first).
  var recent=document.getElementById('recent');
  pts.slice(-10).reverse().forEach(function(p){
    var when=p.when?new Date(p.when).toLocaleDateString():'';
    var row=document.createElement('div'); row.className='rec';
    row.innerHTML='<span>'+esc(p.kind)+' \u00b7 '+esc(p.label)+' <span style=\""color:#94a3b8\"">'+esc(when)+'</span></span>'+
      '<span class=\""badge\"" style=\""background:'+color(p.score)+'\"">'+p.score+'/100</span>';
    recent.appendChild(row);
  });

  function esc(s){ return String(s==null?'':s).replace(/[&<>]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;'}[c];}); }
})();
</script>");
    }
}
