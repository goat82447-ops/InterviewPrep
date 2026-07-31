using System.Net;
using System.Text;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure;

namespace InterviewPrep.Web;

/// <summary>Renders the "Live interview" page: a webcam mock interview where the
/// app reads the rules and a question out loud, listens to your spoken answer,
/// then tells you selected/rejected, your drawbacks, and where to improve.</summary>
internal static class LivePage
{
    public static string Render(
        string? topic, string question, bool aiEnabled,
        IReadOnlyList<AiProvider> models, string? selectedModel)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Live Interview</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        // Hero
        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83d\udcf9</span><div>");
        sb.Append("<div class=\"brand-name\">Live Interview</div>");
        sb.Append("<div class=\"brand-tag\">Camera on \u00b7 hear the rules &amp; question \u00b7 answer out loud \u00b7 get selected or rejected</div>");
        sb.Append("</div></div>");
        sb.Append($"<span class=\"mode\">{(aiEnabled ? "AI interviewer: on" : "AI: off (offline check)")}</span>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Nav
        sb.Append("<div class=\"nav\">");
        sb.Append("<a class=\"chip\" href=\"/intro\">\ud83d\ude4b Self intro</a>");
        sb.Append("<a class=\"chip\" href=\"/ask\">\ud83d\udca1 Ask &amp; Learn</a>");
        sb.Append("<a class=\"chip\" href=\"/practice\">\ud83c\udf93 Practice questions</a>");
        sb.Append("<a class=\"chip\" href=\"/mock\">\ud83c\udf99\ufe0f Mock interview</a>");
        sb.Append("<a class=\"chip active\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("</div>");

        // Topic chips
        sb.Append("<div class=\"topics\">");
        var randomActive = string.IsNullOrWhiteSpace(topic) ? " active" : string.Empty;
        sb.Append($"<a class=\"chip{randomActive}\" href=\"/live\">\ud83c\udfb2 Any topic</a>");
        foreach (var t in QuestionBank.Topics)
        {
            var active = string.Equals(t, topic, StringComparison.OrdinalIgnoreCase) ? " active" : string.Empty;
            sb.Append($"<a class=\"chip{active}\" href=\"/live?topic={WebUtility.UrlEncode(t)}\">{WebUtility.HtmlEncode(t)}</a>");
        }

        sb.Append("</div>");

        // Rules card
        sb.Append("<div class=\"rules\">");
        sb.Append("<div class=\"rules-title\">\ud83d\udccb Interview rules</div>");
        sb.Append("<ol>");
        sb.Append("<li>Turn on your camera and sit straight, like a real interview.</li>");
        sb.Append("<li>Press <b>Start</b>. You will hear the rules and then the question.</li>");
        sb.Append("<li>Press <b>Answer</b> and speak your answer out loud, clearly.</li>");
        sb.Append("<li>Press <b>Stop &amp; submit</b> when done. Wait for the result.</li>");
        sb.Append("<li>Read whether you are <b>selected</b> or <b>rejected</b>, your drawbacks, and how to improve.</li>");
        sb.Append("</ol></div>");

        // Camera + controls
        sb.Append("<div class=\"stage\">");
        sb.Append("<div class=\"camwrap\"><video id=\"cam\" autoplay playsinline muted></video>");
        sb.Append("<div id=\"reclamp\" class=\"reclamp\">\u25cf REC</div></div>");

        sb.Append("<div class=\"panel\">");

        // Model picker
        AppendModelPicker(sb, models, selectedModel);

        // Running score across recent answers (filled by JS from localStorage).
        sb.Append("<div id=\"stats\" class=\"stats-bar\" hidden>");
        sb.Append("<span class=\"stat\"><b id=\"statCount\">0</b> answers</span>");
        sb.Append("<span class=\"stat\">Avg (last 5): <b id=\"statAvg\">0</b>/100</span>");
        sb.Append("<span class=\"stat\">Best: <b id=\"statBest\">0</b></span>");
        sb.Append("<span id=\"statTrend\" class=\"stat trend\"></span>");
        sb.Append("<button id=\"statReset\" class=\"stat-reset\" type=\"button\" title=\"Clear score history\">reset</button>");
        sb.Append("</div>");

        sb.Append("<div class=\"qlabel\">Question</div>");
        sb.Append($"<div id=\"question\" class=\"question\">{WebUtility.HtmlEncode(question)}</div>");

        sb.Append("<div class=\"live-label\">What the interviewer heard</div>");
        sb.Append("<div id=\"transcript\" class=\"transcript\" contenteditable=\"true\" data-placeholder=\"Your spoken answer appears here. You can also type or fix it.\"></div>");

        sb.Append("<div class=\"controls\">");
        sb.Append("<button id=\"startBtn\" class=\"btn btn-primary\" type=\"button\">\u25b6 Start</button>");
        sb.Append("<button id=\"answerBtn\" class=\"btn btn-mic\" type=\"button\" disabled>\ud83c\udf99\ufe0f Answer</button>");
        sb.Append("<button id=\"submitBtn\" class=\"btn btn-go\" type=\"button\" disabled>\u23f9 Stop &amp; submit</button>");
        sb.Append("<button id=\"nextBtn\" class=\"btn btn-ghost\" type=\"button\">\u21bb Next question</button>");
        sb.Append("</div>");

        sb.Append("<div id=\"status\" class=\"status\">Press Start when you are ready.</div>");
        sb.Append("</div>"); // panel
        sb.Append("</div>"); // stage

        // Result (filled by JS)
        sb.Append("<div id=\"result\" class=\"result\" hidden></div>");

        // Hidden state
        sb.Append($"<input type=\"hidden\" id=\"topic\" value=\"{WebUtility.HtmlEncode(topic ?? string.Empty)}\">");

        sb.Append("</main>");

        AppendScript(sb);

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendModelPicker(StringBuilder sb, IReadOnlyList<AiProvider> models, string? selectedModel)
    {
        sb.Append("<div class=\"modelrow\"><span class=\"mlabel\">\ud83e\udde0 Interviewer model</span>");
        sb.Append("<select id=\"model\" class=\"model\">");
        foreach (var m in models)
        {
            var sel = string.Equals(m.Id, selectedModel, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty;
            if (m.HasKey)
            {
                sb.Append($"<option value=\"{WebUtility.HtmlEncode(m.Id)}\"{sel}>{WebUtility.HtmlEncode(m.DisplayName)}</option>");
            }
            else
            {
                sb.Append($"<option value=\"{WebUtility.HtmlEncode(m.Id)}\" disabled>{WebUtility.HtmlEncode(m.DisplayName)} \u2014 add API key</option>");
            }
        }

        sb.Append("</select></div>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#4338ca,#0891b2);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:900px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".mode{background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.35);padding:6px 12px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap;}");
        sb.Append(".wrap{max-width:900px;margin:auto;padding:18px 18px 60px;}");
        sb.Append(".nav,.topics{display:flex;flex-wrap:wrap;gap:8px;margin:14px 0;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".chip.active{background:#0891b2;border-color:#0891b2;color:#fff;}");
        sb.Append(".rules{background:#eef2ff;border:1px solid #c7d2fe;border-radius:16px;padding:16px 20px;margin-top:8px;}");
        sb.Append(".rules-title{font-weight:800;margin-bottom:6px;}");
        sb.Append(".rules ol{margin:0;padding-left:20px;}.rules li{margin:4px 0;font-size:14px;}");
        sb.Append(".stage{display:flex;gap:16px;margin-top:16px;flex-wrap:wrap;}");
        sb.Append(".camwrap{position:relative;flex:1 1 320px;min-width:280px;background:#0f172a;border-radius:16px;overflow:hidden;aspect-ratio:4/3;}");
        sb.Append("#cam{width:100%;height:100%;object-fit:cover;transform:scaleX(-1);}");
        sb.Append(".reclamp{position:absolute;top:10px;left:10px;background:rgba(220,38,38,.9);color:#fff;font-size:12px;font-weight:700;padding:4px 10px;border-radius:999px;display:none;}");
        sb.Append(".reclamp.on{display:block;animation:blink 1s infinite;}");
        sb.Append("@keyframes blink{50%{opacity:.35;}}");
        sb.Append(".panel{flex:1 1 320px;min-width:280px;background:#fff;border-radius:16px;padding:18px;box-shadow:0 1px 3px rgba(15,23,42,.07);}");
        sb.Append(".modelrow{display:flex;align-items:center;gap:10px;margin-bottom:12px;flex-wrap:wrap;}");
        sb.Append(".mlabel{font-size:13px;font-weight:700;color:#475569;}");
        sb.Append(".model{border:1px solid #cbd5e1;border-radius:10px;padding:9px 12px;font-size:14px;font-family:inherit;font-weight:600;color:#0f172a;background:#fff;cursor:pointer;}");
        sb.Append(".model:focus{outline:none;border-color:#0891b2;box-shadow:0 0 0 3px rgba(8,145,178,.15);}");
        sb.Append(".stats-bar{display:flex;align-items:center;gap:14px;flex-wrap:wrap;background:#f0fdfa;border:1px solid #99f6e4;border-radius:12px;padding:8px 14px;margin-bottom:12px;font-size:13px;color:#0f766e;}");
        sb.Append(".stat b{color:#0f172a;font-size:15px;}");
        sb.Append(".stat.trend{font-weight:700;}");
        sb.Append(".stat-reset{margin-left:auto;background:none;border:none;color:#0891b2;font-weight:700;font-size:12px;cursor:pointer;text-decoration:underline;font-family:inherit;}");
        sb.Append(".qlabel,.live-label{font-size:12px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.04em;margin-top:6px;}");
        sb.Append(".question{font-size:17px;font-weight:700;color:#0f172a;margin:6px 0 12px;line-height:1.4;}");
        sb.Append(".transcript{min-height:74px;border:1px solid #cbd5e1;border-radius:12px;padding:10px 12px;font-size:14.5px;line-height:1.5;background:#f8fafc;margin:6px 0 12px;}");
        sb.Append(".transcript:focus{outline:none;border-color:#0891b2;box-shadow:0 0 0 3px rgba(8,145,178,.15);background:#fff;}");
        sb.Append(".transcript:empty:before{content:attr(data-placeholder);color:#94a3b8;}");
        sb.Append(".controls{display:flex;flex-wrap:wrap;gap:8px;}");
        sb.Append(".btn{border:none;border-radius:10px;padding:10px 16px;font-size:14px;font-weight:700;font-family:inherit;cursor:pointer;}");
        sb.Append(".btn:disabled{opacity:.5;cursor:not-allowed;}");
        sb.Append(".btn-primary{background:#4338ca;color:#fff;}.btn-primary:hover:not(:disabled){background:#3730a3;}");
        sb.Append(".btn-mic{background:#f1f5f9;color:#334155;}.btn-mic:hover:not(:disabled){background:#e2e8f0;}");
        sb.Append(".btn-mic.rec{background:#dc2626;color:#fff;}");
        sb.Append(".btn-go{background:#0891b2;color:#fff;}.btn-go:hover:not(:disabled){background:#0e7490;}");
        sb.Append(".btn-ghost{background:#fff;border:1px solid #cbd5e1;color:#334155;}.btn-ghost:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".status{margin-top:12px;font-size:13.5px;color:#475569;min-height:20px;}");
        sb.Append(".result{margin-top:16px;background:#fff;border-radius:16px;padding:22px;box-shadow:0 1px 3px rgba(15,23,42,.07);border-left:6px solid #94a3b8;}");
        sb.Append(".result.sel{border-left-color:#10b981;}.result.rej{border-left-color:#ef4444;}.result.bor{border-left-color:#f59e0b;}");
        sb.Append(".verdict{display:flex;align-items:center;gap:12px;flex-wrap:wrap;}");
        sb.Append(".badge{font-size:15px;font-weight:800;padding:6px 14px;border-radius:999px;color:#fff;}");
        sb.Append(".badge.sel{background:#10b981;}.badge.rej{background:#ef4444;}.badge.bor{background:#f59e0b;}");
        sb.Append(".score{font-size:14px;font-weight:700;color:#334155;}");
        sb.Append(".rfb{margin:12px 0;font-size:15px;line-height:1.5;}");
        sb.Append(".rsec{margin-top:14px;}.rsec h4{margin:0 0 6px;font-size:13px;text-transform:uppercase;letter-spacing:.04em;color:#64748b;}");
        sb.Append(".rsec ul{margin:0;padding-left:20px;}.rsec li{margin:4px 0;font-size:14.5px;line-height:1.45;}");
        sb.Append(".model-ans{background:#ecfeff;border:1px solid #a5f3fc;border-radius:12px;padding:14px 16px;margin-top:14px;font-size:14.5px;line-height:1.55;white-space:pre-wrap;}");
        sb.Append("@media(max-width:560px){.hero-inner{flex-wrap:wrap;}}");
        sb.Append("</style>");
    }

    private static void AppendScript(StringBuilder sb)
    {
        sb.Append("<script>");
        sb.Append(@"
(function(){
  var camEl=document.getElementById('cam');
  var recLamp=document.getElementById('reclamp');
  var startBtn=document.getElementById('startBtn');
  var answerBtn=document.getElementById('answerBtn');
  var submitBtn=document.getElementById('submitBtn');
  var nextBtn=document.getElementById('nextBtn');
  var statusEl=document.getElementById('status');
  var questionEl=document.getElementById('question');
  var transcriptEl=document.getElementById('transcript');
  var resultEl=document.getElementById('result');
  var topicEl=document.getElementById('topic');
  var modelEl=document.getElementById('model');
  var recog=null, listening=false, finalText='';

  var statsEl=document.getElementById('stats');
  var statCount=document.getElementById('statCount');
  var statAvg=document.getElementById('statAvg');
  var statBest=document.getElementById('statBest');
  var statTrend=document.getElementById('statTrend');
  var statReset=document.getElementById('statReset');
  var STORE='liveScores';

  function loadScores(){ try{ return JSON.parse(localStorage.getItem(STORE)||'[]'); }catch(e){ return []; } }
  function saveScores(a){ try{ localStorage.setItem(STORE, JSON.stringify(a.slice(-50))); }catch(e){} }

  function renderStats(){
    var a=loadScores();
    if(!a.length){ statsEl.hidden=true; return; }
    statsEl.hidden=false;
    var last5=a.slice(-5);
    var avg=Math.round(last5.reduce(function(s,x){return s+x;},0)/last5.length);
    statCount.textContent=a.length;
    statAvg.textContent=avg;
    statBest.textContent=Math.max.apply(null,a);
    if(a.length>=2){
      var diff=a[a.length-1]-a[a.length-2];
      statTrend.textContent=diff>0?('\u2191 +'+diff+' vs last'):(diff<0?('\u2193 '+diff+' vs last'):'\u2192 same as last');
      statTrend.style.color=diff>0?'#059669':(diff<0?'#dc2626':'#64748b');
    } else { statTrend.textContent=''; }
  }

  function pushScore(n){ var a=loadScores(); a.push(Math.max(0,Math.min(100,n|0))); saveScores(a); renderStats(); }

  if(statReset){ statReset.addEventListener('click',function(){ saveScores([]); renderStats(); }); }
  renderStats();

  function setStatus(t){ statusEl.textContent=t; }

  function speak(text, after){
    try{
      if(!('speechSynthesis' in window)){ if(after) after(); return; }
      window.speechSynthesis.cancel();
      var u=new SpeechSynthesisUtterance(text);
      u.rate=1; u.pitch=1;
      if(after) u.onend=after;
      window.speechSynthesis.speak(u);
    }catch(e){ if(after) after(); }
  }

  async function startCamera(){
    try{
      var stream=await navigator.mediaDevices.getUserMedia({video:true,audio:false});
      camEl.srcObject=stream;
      return true;
    }catch(e){
      setStatus('Camera blocked. Allow camera access, or continue with voice only.');
      return false;
    }
  }

  function setupRecognition(){
    var SR=window.SpeechRecognition||window.webkitSpeechRecognition;
    if(!SR){ return null; }
    var r=new SR();
    r.lang='en-US'; r.continuous=true; r.interimResults=true;
    r.onresult=function(ev){
      var interim='';
      for(var i=ev.resultIndex;i<ev.results.length;i++){
        var t=ev.results[i][0].transcript;
        if(ev.results[i].isFinal){ finalText+=t+' '; } else { interim+=t; }
      }
      transcriptEl.textContent=(finalText+interim).trim();
    };
    r.onerror=function(ev){ setStatus('Mic error: '+ev.error+'. You can type your answer instead.'); };
    r.onend=function(){ if(listening){ try{ r.start(); }catch(e){} } };
    return r;
  }

  var RULES='Welcome to your live interview. Here are the rules. '+
    'I will ask you one question. Press Answer and speak clearly. '+
    'Press Stop and submit when you are done. I will then tell you if you are selected or rejected, '+
    'your drawbacks, and how to improve. Here is your question.';

  startBtn.addEventListener('click', async function(){
    startBtn.disabled=true;
    setStatus('Starting camera...');
    await startCamera();
    setStatus('Reading the rules...');
    var q=questionEl.textContent.trim();
    speak(RULES, function(){
      speak(q, function(){
        setStatus('Press Answer and speak your answer.');
        answerBtn.disabled=false;
      });
    });
  });

  answerBtn.addEventListener('click', function(){
    if(!recog){ recog=setupRecognition(); }
    finalText=transcriptEl.textContent?transcriptEl.textContent+' ':'';
    if(recog){
      listening=true;
      try{ recog.start(); }catch(e){}
      setStatus('Listening... speak now. Press Stop and submit when done.');
    }else{
      setStatus('Speech-to-text not supported in this browser. Type your answer, then Stop and submit.');
    }
    answerBtn.classList.add('rec');
    answerBtn.disabled=true;
    recLamp.classList.add('on');
    submitBtn.disabled=false;
  });

  submitBtn.addEventListener('click', async function(){
    listening=false;
    if(recog){ try{ recog.stop(); }catch(e){} }
    answerBtn.classList.remove('rec');
    recLamp.classList.remove('on');
    submitBtn.disabled=true;
    var answer=(transcriptEl.textContent||'').trim();
    if(!answer){ setStatus('No answer captured. Speak or type something, then submit again.'); submitBtn.disabled=false; return; }
    setStatus('Interviewer is judging your answer...');
    try{
      var body=new URLSearchParams();
      body.set('topic', topicEl.value||'');
      body.set('question', questionEl.textContent.trim());
      body.set('answer', answer);
      body.set('model', modelEl?modelEl.value:'');
      var res=await fetch('/live-json',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:body.toString()});
      var v=await res.json();
      showResult(v);
    }catch(e){
      setStatus('Could not reach the interviewer. Try again.');
      submitBtn.disabled=false;
    }
  });

  function esc(s){ var d=document.createElement('div'); d.textContent=s==null?'':s; return d.innerHTML; }

  function showResult(v){
    var cls=v.verdict==='Selected'?'sel':(v.verdict==='Rejected'?'rej':'bor');
    var h='<div class=""verdict""><span class=""badge '+cls+'"">'+esc(v.verdict)+'</span>'+
          '<span class=""score"">Score: '+(v.score||0)+' / 100</span></div>'+
          '<div class=""rfb"">'+esc(v.feedback)+'</div>';
    if(v.drawbacks&&v.drawbacks.length){
      h+='<div class=""rsec""><h4>\u26a0\ufe0f Your drawbacks</h4><ul>';
      v.drawbacks.forEach(function(d){ h+='<li>'+esc(d)+'</li>'; });
      h+='</ul></div>';
    }
    if(v.improve&&v.improve.length){
      h+='<div class=""rsec""><h4>\ud83d\ude80 Where to improve</h4><ul>';
      v.improve.forEach(function(d){ h+='<li>'+esc(d)+'</li>'; });
      h+='</ul></div>';
    }
    if(v.modelAnswer){
      h+='<div class=""rsec""><h4>\u2705 Strong model answer</h4><div class=""model-ans"">'+esc(v.modelAnswer)+'</div></div>';
    }
    resultEl.className='result '+cls;
    resultEl.innerHTML=h;
    resultEl.hidden=false;
    resultEl.scrollIntoView({behavior:'smooth',block:'start'});
    setStatus('Done. Press Next question to continue, or Start again on a new topic.');
    pushScore(v.score||0);
    speak(v.verdict+'. '+(v.feedback||''));
  }

  nextBtn.addEventListener('click', async function(){
    try{ window.speechSynthesis.cancel(); }catch(e){}
    setStatus('Loading next question...');
    try{
      var res=await fetch('/live/question?topic='+encodeURIComponent(topicEl.value||''));
      var d=await res.json();
      questionEl.textContent=d.question;
      transcriptEl.textContent='';
      finalText='';
      resultEl.hidden=true;
      answerBtn.disabled=true;
      submitBtn.disabled=true;
      startBtn.disabled=false;
      setStatus('New question ready. Press Start to hear it, then Answer.');
    }catch(e){ setStatus('Could not load a new question.'); }
  });
})();
");
        sb.Append("</script>");
    }
}
