using System.Net;
using System.Text;
using InterviewPrep.Infrastructure;
using InterviewPrep.Services;

namespace InterviewPrep.Web;

/// <summary>Renders the "Interview mode" page: paste or upload your resume and
/// tech stack, then run a full four-round mock interview (two technical rounds,
/// one managerial round, one HR round) where every question is generated from
/// your own resume and each answer is scored with concrete tips.</summary>
internal static class InterviewPage
{
    public static string Render(bool aiEnabled, IReadOnlyList<AiProvider> models, string? selectedModel)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>Interview mode</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        // Hero
        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">\ud83e\udde9</span><div>");
        sb.Append("<div class=\"brand-name\">Interview mode</div>");
        sb.Append("<div class=\"brand-tag\">Upload your resume &amp; tech stack \u00b7 2 tech rounds \u00b7 1 managerial \u00b7 1 HR \u00b7 real questions from YOUR resume</div>");
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
        sb.Append("<a class=\"chip\" href=\"/live\">\ud83d\udcf9 Live interview</a>");
        sb.Append("<a class=\"chip active\" href=\"/interview\">\ud83e\udde9 Interview mode</a>");
        sb.Append("<a class=\"chip\" href=\"/drills\">\u26a1 Rapid drills</a>");
        sb.Append("<a class=\"chip\" href=\"/plan\">\ud83d\uddd3\ufe0f Study plan</a>");
        sb.Append("<a class=\"chip\" href=\"/settings\">\u2699\ufe0f Settings</a>");
        sb.Append("</div>");

        // Round progress bar
        sb.Append("<div id=\"progress\" class=\"progress\">");
        AppendRoundChip(sb, 0, "\ud83e\udde0", "Technical Round 1", "adaptive \u00b7 2\u20135 Q");
        AppendRoundChip(sb, 1, "\ud83c\udfd7\ufe0f", "Technical Round 2", "adaptive \u00b7 2\u20135 Q");
        AppendRoundChip(sb, 2, "\ud83d\udc54", "Managerial Round", "adaptive \u00b7 1\u20133 Q");
        AppendRoundChip(sb, 3, "\ud83e\udd1d", "HR Round", "adaptive \u00b7 1\u20133 Q");
        sb.Append("</div>");

        // Setup card
        sb.Append("<div id=\"setup\" class=\"card\">");
        sb.Append("<div class=\"card-title\">1 \u00b7 Add your resume &amp; tech stack</div>");
        sb.Append("<div class=\"hint\">Paste your resume text below, or upload a .txt file. For PDF/Word, open it, copy all the text and paste it here. The interviewer reads only this \u2014 nothing is uploaded to any server except your chosen AI model.</div>");

        sb.Append("<label class=\"flabel\">Your resume</label>");
        sb.Append("<textarea id=\"resume\" class=\"ta\" rows=\"9\" placeholder=\"Paste your full resume here: experience, projects, skills, education...\"></textarea>");

        sb.Append("<div class=\"filerow\">");
        sb.Append("<label class=\"filebtn\">\ud83d\udcc4 Upload .txt resume<input type=\"file\" id=\"resumeFile\" accept=\".txt,.md,.text\" hidden></label>");
        sb.Append("<span id=\"fileName\" class=\"filename\"></span>");
        sb.Append("</div>");

        sb.Append("<label class=\"flabel\">Tech stack / target role</label>");
        sb.Append("<input id=\"stack\" class=\"inp\" type=\"text\" placeholder=\"e.g. C#, .NET, Azure, SQL, React \u2014 Backend Engineer\">");

        AppendModelPicker(sb, models, selectedModel);

        sb.Append("<button id=\"startBtn\" class=\"btn btn-primary\" type=\"button\">\u25b6 Start interview</button>");
        sb.Append("<div id=\"setupMsg\" class=\"status\"></div>");
        sb.Append("</div>"); // setup

        // Stage card (hidden until started)
        sb.Append("<div id=\"stage\" class=\"card\" hidden>");
        sb.Append("<div class=\"round-head\"><div id=\"roundName\" class=\"round-name\">Technical Round 1</div>");
        sb.Append("<div id=\"roundStep\" class=\"round-step\">Question 1</div></div>");

        sb.Append("<div class=\"qlabel\">Interviewer asks</div>");
        sb.Append("<div id=\"question\" class=\"question\">\u2026</div>");

        sb.Append("<label class=\"flabel\">Your answer</label>");
        sb.Append("<textarea id=\"answer\" class=\"ta\" rows=\"6\" placeholder=\"Type your answer, or press the mic and speak it.\"></textarea>");

        sb.Append("<div class=\"controls\">");
        sb.Append("<button id=\"micBtn\" class=\"btn btn-mic\" type=\"button\">\ud83c\udf99\ufe0f Speak</button>");
        sb.Append("<button id=\"submitBtn\" class=\"btn btn-go\" type=\"button\">Submit answer</button>");
        sb.Append("<button id=\"nextBtn\" class=\"btn btn-primary\" type=\"button\" hidden>Next \u2192</button>");
        sb.Append("</div>");

        sb.Append("<div id=\"turnStatus\" class=\"status\"></div>");
        sb.Append("<div id=\"feedback\" class=\"feedback\" hidden></div>");
        sb.Append("</div>"); // stage

        // Final card (hidden)
        sb.Append("<div id=\"final\" class=\"card\" hidden></div>");

        sb.Append("</main>");

        AppendScript(sb);

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void AppendRoundChip(StringBuilder sb, int index, string emoji, string name, string sub)
    {
        sb.Append($"<div class=\"pchip todo\" id=\"pchip-{index}\">");
        sb.Append($"<span class=\"pemoji\">{emoji}</span>");
        sb.Append($"<span class=\"pname\">{WebUtility.HtmlEncode(name)}</span>");
        sb.Append($"<span class=\"psub\">{WebUtility.HtmlEncode(sub)}</span>");
        sb.Append("</div>");
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
        sb.Append(".hero{background:linear-gradient(120deg,#7c3aed,#0891b2);color:#fff;padding:26px 24px;}");
        sb.Append(".hero-inner{max-width:900px;margin:auto;display:flex;align-items:center;gap:16px;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;flex:1;}");
        sb.Append(".logo{font-size:34px;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.9;margin-top:2px;}");
        sb.Append(".mode{background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.35);padding:6px 12px;border-radius:999px;font-size:12px;font-weight:600;white-space:nowrap;}");
        sb.Append(".wrap{max-width:900px;margin:auto;padding:18px 18px 60px;}");
        sb.Append(".nav{display:flex;flex-wrap:wrap;gap:8px;margin:14px 0;}");
        sb.Append(".chip{background:#fff;border:1px solid #e2e8f0;border-radius:999px;padding:8px 14px;font-size:13.5px;font-weight:600;color:#334155;text-decoration:none;}");
        sb.Append(".chip:hover{border-color:#0891b2;color:#0891b2;}");
        sb.Append(".chip.active{background:#0891b2;border-color:#0891b2;color:#fff;}");
        sb.Append(".progress{display:flex;gap:10px;flex-wrap:wrap;margin:8px 0 16px;}");
        sb.Append(".pchip{flex:1 1 180px;background:#fff;border:1px solid #e2e8f0;border-radius:14px;padding:12px 14px;display:flex;flex-direction:column;gap:2px;position:relative;}");
        sb.Append(".pchip .pemoji{font-size:20px;}");
        sb.Append(".pchip .pname{font-weight:700;font-size:14px;}");
        sb.Append(".pchip .psub{font-size:12px;color:#64748b;}");
        sb.Append(".pchip.active{border-color:#7c3aed;box-shadow:0 0 0 2px rgba(124,58,237,.25);}");
        sb.Append(".pchip.done{background:#ecfdf5;border-color:#6ee7b7;}");
        sb.Append(".pchip.done::after{content:'\\2713';position:absolute;top:10px;right:12px;color:#059669;font-weight:800;}");
        sb.Append(".card{background:#fff;border-radius:16px;padding:18px 20px;margin-top:14px;box-shadow:0 1px 3px rgba(15,23,42,.07);}");
        sb.Append(".card-title{font-weight:800;font-size:16px;margin-bottom:6px;}");
        sb.Append(".hint{font-size:13px;color:#64748b;margin-bottom:12px;line-height:1.5;}");
        sb.Append(".flabel{display:block;font-size:12.5px;font-weight:700;color:#475569;margin:12px 0 6px;text-transform:uppercase;letter-spacing:.03em;}");
        sb.Append(".ta,.inp{width:100%;border:1px solid #cbd5e1;border-radius:12px;padding:11px 13px;font-family:inherit;font-size:14.5px;color:#0f172a;background:#f8fafc;}");
        sb.Append(".ta:focus,.inp:focus{outline:none;border-color:#7c3aed;background:#fff;}");
        sb.Append(".ta{resize:vertical;line-height:1.5;}");
        sb.Append(".filerow{display:flex;align-items:center;gap:12px;margin-top:10px;flex-wrap:wrap;}");
        sb.Append(".filebtn{background:#eef2ff;border:1px solid #c7d2fe;color:#4338ca;border-radius:999px;padding:8px 14px;font-size:13px;font-weight:700;cursor:pointer;}");
        sb.Append(".filebtn:hover{background:#e0e7ff;}");
        sb.Append(".filename{font-size:12.5px;color:#059669;font-weight:600;}");
        sb.Append(".modelrow{display:flex;align-items:center;gap:10px;margin:14px 0 4px;flex-wrap:wrap;}");
        sb.Append(".mlabel{font-size:13px;font-weight:700;color:#475569;}");
        sb.Append(".model{border:1px solid #cbd5e1;border-radius:10px;padding:8px 12px;font-family:inherit;font-size:14px;background:#fff;}");
        sb.Append(".controls{display:flex;gap:10px;margin-top:14px;flex-wrap:wrap;}");
        sb.Append(".btn{border:none;border-radius:12px;padding:12px 20px;font-size:14.5px;font-weight:700;cursor:pointer;font-family:inherit;}");
        sb.Append(".btn-primary{background:linear-gradient(120deg,#7c3aed,#0891b2);color:#fff;margin-top:16px;}");
        sb.Append(".btn-go{background:#0891b2;color:#fff;}");
        sb.Append(".btn-mic{background:#eef2ff;color:#4338ca;border:1px solid #c7d2fe;}");
        sb.Append(".btn-mic.on{background:#dc2626;color:#fff;border-color:#dc2626;animation:blink 1s infinite;}");
        sb.Append("@keyframes blink{50%{opacity:.55;}}");
        sb.Append(".btn:disabled{opacity:.5;cursor:not-allowed;}");
        sb.Append(".round-head{display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap;border-bottom:1px solid #eef2f7;padding-bottom:12px;margin-bottom:12px;}");
        sb.Append(".round-name{font-size:18px;font-weight:800;color:#7c3aed;}");
        sb.Append(".round-step{font-size:13px;font-weight:700;color:#64748b;background:#f1f5f9;border-radius:999px;padding:5px 12px;}");
        sb.Append(".qlabel{font-size:12px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.04em;}");
        sb.Append(".question{font-size:17px;font-weight:700;line-height:1.5;margin:6px 0 4px;color:#0f172a;}");
        sb.Append(".status{font-size:13px;color:#64748b;margin-top:10px;min-height:18px;}");
        sb.Append(".feedback{margin-top:14px;border-radius:14px;padding:14px 16px;border:1px solid #e2e8f0;background:#f8fafc;}");
        sb.Append(".fb-score{display:flex;align-items:center;gap:10px;font-weight:800;font-size:15px;margin-bottom:8px;}");
        sb.Append(".fb-badge{border-radius:999px;padding:3px 12px;font-size:13px;color:#fff;}");
        sb.Append(".fb-text{font-size:14px;line-height:1.55;color:#334155;}");
        sb.Append(".fb-tips{margin:10px 0 0;padding-left:18px;}");
        sb.Append(".fb-tips li{font-size:13.5px;margin:4px 0;color:#475569;}");
        sb.Append(".sum-title{font-size:19px;font-weight:800;margin-bottom:4px;}");
        sb.Append(".sum-verdict{font-size:15px;font-weight:700;margin:8px 0 14px;padding:10px 14px;border-radius:12px;}");
        sb.Append(".sum-row{display:flex;justify-content:space-between;align-items:center;padding:10px 0;border-bottom:1px solid #eef2f7;font-size:14px;}");
        sb.Append(".sum-row b{font-weight:700;}");
        sb.Append(".bar{flex:1;height:8px;background:#e2e8f0;border-radius:999px;margin:0 12px;overflow:hidden;max-width:220px;}");
        sb.Append(".bar span{display:block;height:100%;background:linear-gradient(90deg,#7c3aed,#0891b2);}");
        sb.Append("@media(max-width:640px){.hero-inner{flex-direction:column;align-items:flex-start;}.pchip{flex:1 1 100%;}.controls{flex-direction:column;}.controls .btn{width:100%;}}");
        sb.Append("</style>");
    }

    private static void AppendScript(StringBuilder sb)
    {
        sb.Append(@"<script>
(function(){
  // Rounds are adaptive: each keeps asking based on how the candidate answers,
  // between a minimum and maximum number of questions.
  var ROUNDS=[
    {id:'tech1',name:'Technical Round 1',min:2,max:5},
    {id:'tech2',name:'Technical Round 2',min:2,max:5},
    {id:'managerial',name:'Managerial Round',min:1,max:3},
    {id:'hr',name:'HR Round',min:1,max:3}
  ];
  var resume='',stack='',model='';
  var ri=0, curQ='', asked=[], roundScores=[], allScores=[], roundResults=[];

  var $=function(id){return document.getElementById(id);};
  var resumeEl=$('resume'), fileEl=$('resumeFile'), fileName=$('fileName'), stackEl=$('stack'), modelEl=$('model');
  var setupCard=$('setup'), setupMsg=$('setupMsg'), startBtn=$('startBtn');
  var stageCard=$('stage'), roundNameEl=$('roundName'), roundStepEl=$('roundStep'), qEl=$('question');
  var answerEl=$('answer'), micBtn=$('micBtn'), submitBtn=$('submitBtn'), nextBtn=$('nextBtn');
  var turnStatus=$('turnStatus'), fbEl=$('feedback'), finalCard=$('final');

  // Upload a .txt resume into the textarea.
  fileEl.addEventListener('change',function(){
    var f=fileEl.files[0]; if(!f){return;}
    var r=new FileReader();
    r.onload=function(){ resumeEl.value=r.result||''; fileName.textContent='Loaded: '+f.name; };
    r.onerror=function(){ fileName.textContent='Could not read that file \u2014 paste the text instead.'; };
    r.readAsText(f);
  });

  function post(url,data){
    var body=new URLSearchParams(data);
    return fetch(url,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:body})
      .then(function(r){return r.json();});
  }

  function setProgress(){
    for(var i=0;i<ROUNDS.length;i++){
      var c=$('pchip-'+i); if(!c){continue;}
      c.className='pchip '+(i<ri?'done':(i===ri?'active':'todo'));
    }
  }

  function badgeColor(s){ return s>=75?'#059669':(s>=55?'#d97706':'#dc2626'); }

  // Decide if this round is done, based on how the candidate is answering.
  // Strong answers end the round early; borderline answers get more questions
  // (up to the cap) to give a fair chance; a clearly failing run also stops.
  function shouldEndRound(){
    var r=ROUNDS[ri], n=asked.length;
    if(n<r.min){ return false; }
    if(n>=r.max){ return true; }
    var avg=roundScores.reduce(function(a,b){return a+b;},0)/Math.max(1,roundScores.length);
    if(avg>=75){ return true; }
    if(avg<40){ return true; }
    return false;
  }

  function startInterview(){
    resume=(resumeEl.value||'').trim();
    stack=(stackEl.value||'').trim();
    model=modelEl?modelEl.value:'';
    if(resume.length<40){ setupMsg.textContent='Please paste a bit more of your resume first (experience, projects, skills).'; return; }
    setupCard.hidden=true; stageCard.hidden=false; finalCard.hidden=true;
    ri=0; allScores=[]; roundResults=[];
    beginRound();
    stageCard.scrollIntoView({behavior:'smooth',block:'start'});
  }

  function beginRound(){
    asked=[]; roundScores=[]; setProgress();
    roundNameEl.textContent=ROUNDS[ri].name;
    fetchQuestion();
  }

  function fetchQuestion(){
    fbEl.hidden=true; nextBtn.hidden=true; submitBtn.hidden=false;
    answerEl.value=''; answerEl.disabled=true; submitBtn.disabled=true;
    roundStepEl.textContent='Question '+(asked.length+1);
    qEl.textContent='Thinking of a question from your resume\u2026';
    turnStatus.textContent='';
    post('/interview/question',{resume:resume,stack:stack,round:ROUNDS[ri].id,asked:asked.join('\n'),model:model})
      .then(function(d){
        curQ=(d&&d.question)?d.question:'Tell me about a recent project from your resume.';
        qEl.textContent=curQ;
        answerEl.disabled=false; submitBtn.disabled=false; answerEl.focus();
      })
      .catch(function(){
        curQ='Tell me about a recent project from your resume.';
        qEl.textContent=curQ; answerEl.disabled=false; submitBtn.disabled=false;
      });
  }

  function submitAnswer(){
    var answer=(answerEl.value||'').trim();
    if(answer.length===0){ turnStatus.textContent='Type or speak your answer first.'; return; }
    submitBtn.disabled=true; answerEl.disabled=true; turnStatus.textContent='Scoring your answer\u2026';
    post('/interview/evaluate',{resume:resume,stack:stack,round:ROUNDS[ri].id,question:curQ,answer:answer,model:model})
      .then(function(d){
        var score=(d&&typeof d.score==='number')?d.score:0;
        var feedback=(d&&d.feedback)?d.feedback:'';
        var tips=(d&&d.tips)?d.tips:[];
        asked.push(curQ); roundScores.push(score); allScores.push(score);
        showFeedback(score,feedback,tips);
        turnStatus.textContent='';
      })
      .catch(function(){
        turnStatus.textContent='Could not reach the interviewer. Check your key in Settings, then try again.';
        submitBtn.disabled=false; answerEl.disabled=false;
      });
  }

  function showFeedback(score,feedback,tips){
    var html='<div class=\""fb-score\"">Score: '+score+'/100 '+
      '<span class=\""fb-badge\"" style=\""background:'+badgeColor(score)+'\"">'+(score>=75?'Strong':(score>=55?'OK':'Weak'))+'</span></div>';
    html+='<div class=\""fb-text\"">'+escapeHtml(feedback)+'</div>';
    if(tips&&tips.length){
      html+='<ul class=\""fb-tips\"">';
      for(var i=0;i<tips.length;i++){ html+='<li>'+escapeHtml(tips[i])+'</li>'; }
      html+='</ul>';
    }
    fbEl.innerHTML=html; fbEl.hidden=false;
    submitBtn.hidden=true; nextBtn.hidden=false;
    var last=shouldEndRound();
    var lastRound=(ri>=ROUNDS.length-1);
    nextBtn.textContent = last ? (lastRound?'See final result \u2192':'Finish round \u2192') : 'Next question \u2192';
    nextBtn.focus();
  }

  function nextStep(){
    if(!shouldEndRound()){ fetchQuestion(); return; }
    // Round finished.
    var avg=Math.round(roundScores.reduce(function(a,b){return a+b;},0)/Math.max(1,roundScores.length));
    roundResults.push({name:ROUNDS[ri].name,avg:avg});
    ri++;
    if(ri<ROUNDS.length){ beginRound(); stageCard.scrollIntoView({behavior:'smooth',block:'start'}); }
    else{ finalSummary(); }
  }

  function finalSummary(){
    setProgress();
    stageCard.hidden=true; finalCard.hidden=false;
    var overall=Math.round(allScores.reduce(function(a,b){return a+b;},0)/Math.max(1,allScores.length));
    var verdict, vColor, vBg;
    if(overall>=75){ verdict='Strong hire \u2014 you are ready.'; vColor='#065f46'; vBg='#ecfdf5'; }
    else if(overall>=60){ verdict='Hire with minor feedback \u2014 close, polish a little.'; vColor='#92400e'; vBg='#fffbeb'; }
    else if(overall>=45){ verdict='Borderline \u2014 keep practising these rounds.'; vColor='#9a3412'; vBg='#fff7ed'; }
    else{ verdict='Not yet \u2014 build more depth and practise again.'; vColor='#991b1b'; vBg='#fef2f2'; }

    var weakest=roundResults.slice().sort(function(a,b){return a.avg-b.avg;})[0];
    var html='<div class=\""sum-title\"">\ud83c\udfc1 Interview complete</div>';
    html+='<div class=\""sum-verdict\"" style=\""color:'+vColor+';background:'+vBg+'\"">Overall '+overall+'/100 \u2014 '+verdict+'</div>';
    for(var i=0;i<roundResults.length;i++){
      var r=roundResults[i];
      html+='<div class=\""sum-row\""><span>'+escapeHtml(r.name)+'</span>'+
        '<span class=\""bar\""><span style=\""width:'+r.avg+'%\""></span></span>'+
        '<b>'+r.avg+'/100</b></div>';
    }
    if(weakest){ html+='<div class=\""hint\"" style=\""margin-top:14px\"">Focus next on <b>'+escapeHtml(weakest.name)+'</b> \u2014 it was your lowest round.</div>'; }
    html+='<button id=\""restartBtn\"" class=\""btn btn-primary\"" type=\""button\"">\u21bb Run another interview</button>';
    finalCard.innerHTML=html; finalCard.hidden=false;
    $('restartBtn').addEventListener('click',function(){
      finalCard.hidden=true; setupCard.hidden=false; setProgressReset();
      window.scrollTo({top:0,behavior:'smooth'});
    });
    finalCard.scrollIntoView({behavior:'smooth',block:'start'});
  }

  function setProgressReset(){
    ri=0;
    for(var i=0;i<ROUNDS.length;i++){ var c=$('pchip-'+i); if(c){c.className='pchip todo';} }
  }

  function escapeHtml(s){
    return String(s==null?'':s).replace(/[&<>\""']/g,function(c){
      return {'&':'&amp;','<':'&lt;','>':'&gt;','\""':'&quot;',""'"":'&#39;'}[c];
    });
  }

  // Optional speech-to-text for the answer box.
  var recog=null, listening=false;
  var SR=window.SpeechRecognition||window.webkitSpeechRecognition;
  if(SR){
    recog=new SR(); recog.lang='en-US'; recog.continuous=true; recog.interimResults=false;
    recog.onresult=function(e){
      var t='';
      for(var i=e.resultIndex;i<e.results.length;i++){ t+=e.results[i][0].transcript+' '; }
      answerEl.value=(answerEl.value+' '+t).trim();
    };
    recog.onend=function(){ if(listening){ try{recog.start();}catch(x){} } };
  } else {
    micBtn.disabled=true; micBtn.title='Speech not supported in this browser \u2014 please type.';
  }
  micBtn.addEventListener('click',function(){
    if(!recog){return;}
    if(listening){ listening=false; recog.stop(); micBtn.classList.remove('on'); micBtn.textContent='\ud83c\udf99\ufe0f Speak'; }
    else{ listening=true; try{recog.start();}catch(x){} micBtn.classList.add('on'); micBtn.textContent='\u23f9 Stop'; answerEl.focus(); }
  });

  startBtn.addEventListener('click',startInterview);
  submitBtn.addEventListener('click',submitAnswer);
  nextBtn.addEventListener('click',nextStep);
  answerEl.addEventListener('keydown',function(e){ if(e.key==='Enter'&&(e.ctrlKey||e.metaKey)){ submitAnswer(); } });
})();
</script>");
    }
}
