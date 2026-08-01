using System.Text;

namespace InterviewPrep.Web;

/// <summary>Shared page chrome: a comfort toolbar (dark mode + bigger text)
/// injected into every page. Preferences are stored in the browser's
/// localStorage so they carry over between pages and visits. The dark and
/// big-text styles are broad overrides keyed off shared class names used by
/// all pages (body, .hero, .wrap, .card, .chip, .nav, inputs), so no per-page
/// colour rewrite is needed.</summary>
internal static class WebChrome
{
    /// <summary>Append the comfort toolbar styles, markup and script. Call this
    /// once on every page, right after <c>&lt;/head&gt;&lt;body&gt;</c>.</summary>
    public static void Append(StringBuilder sb)
    {
        sb.Append(@"<style>
:root{--ui-scale:1;}
html{font-size:calc(16px * var(--ui-scale));}
html[data-big='1']{--ui-scale:1.18;}
html[data-big='2']{--ui-scale:1.34;}

/* Dark mode: broad overrides that work across every page. */
html[data-theme='dark'] body{background:#0b1120 !important;color:#e2e8f0 !important;}
html[data-theme='dark'] .wrap,html[data-theme='dark'] main{background:transparent !important;}
html[data-theme='dark'] .card,html[data-theme='dark'] .panel,html[data-theme='dark'] .stats,
html[data-theme='dark'] .pchip,html[data-theme='dark'] .feedback{
  background:#111a2e !important;border-color:#1e293b !important;color:#e2e8f0 !important;
  box-shadow:none !important;}
html[data-theme='dark'] .chip{background:#111a2e !important;border-color:#1e293b !important;color:#cbd5e1 !important;}
html[data-theme='dark'] .chip.active{background:#1e293b !important;color:#fff !important;}
html[data-theme='dark'] .ta,html[data-theme='dark'] .inp,html[data-theme='dark'] .model,
html[data-theme='dark'] textarea,html[data-theme='dark'] input,html[data-theme='dark'] select{
  background:#0b1120 !important;color:#e2e8f0 !important;border-color:#334155 !important;}
html[data-theme='dark'] .hint,html[data-theme='dark'] .psub,html[data-theme='dark'] .filename,
html[data-theme='dark'] .mlabel,html[data-theme='dark'] .flabel{color:#94a3b8 !important;}
html[data-theme='dark'] .question,html[data-theme='dark'] .qlabel{color:#e2e8f0 !important;}
html[data-theme='dark'] a{color:#93c5fd;}

#comfortBar{position:fixed;right:14px;bottom:14px;z-index:9999;display:flex;gap:8px;
  background:rgba(15,23,42,.86);backdrop-filter:blur(6px);padding:8px;border-radius:14px;
  box-shadow:0 8px 24px rgba(0,0,0,.28);}
#comfortBar button{border:0;cursor:pointer;font:600 13px Inter,system-ui,sans-serif;
  color:#fff;background:#334155;border-radius:10px;padding:8px 11px;line-height:1;}
#comfortBar button:hover{background:#475569;}
#comfortBar button.on{background:#7c3aed;}
</style>");

        sb.Append(@"<div id=""comfortBar"">
  <button id=""cbTheme"" type=""button"" title=""Dark / light mode"">🌙 Dark</button>
  <button id=""cbText"" type=""button"" title=""Bigger text"">A+</button>
</div>");

        sb.Append(@"<script>
(function(){
  var root=document.documentElement;
  var theme=localStorage.getItem('ui-theme')||'light';
  var big=parseInt(localStorage.getItem('ui-big')||'0',10)||0;
  function apply(){
    if(theme==='dark'){ root.setAttribute('data-theme','dark'); } else { root.removeAttribute('data-theme'); }
    if(big>0){ root.setAttribute('data-big',String(big)); } else { root.removeAttribute('data-big'); }
    var t=document.getElementById('cbTheme'), x=document.getElementById('cbText');
    if(t){ t.textContent = theme==='dark' ? '☀️ Light' : '🌙 Dark'; t.classList.toggle('on',theme==='dark'); }
    if(x){ x.textContent = big===0 ? 'A+' : (big===1 ? 'A++' : 'A'); x.classList.toggle('on',big>0); }
  }
  apply();
  document.addEventListener('DOMContentLoaded',function(){
    var t=document.getElementById('cbTheme'), x=document.getElementById('cbText');
    if(t){ t.addEventListener('click',function(){ theme=(theme==='dark'?'light':'dark'); localStorage.setItem('ui-theme',theme); apply(); }); }
    if(x){ x.addEventListener('click',function(){ big=(big+1)%3; localStorage.setItem('ui-big',String(big)); apply(); }); }
  });
})();
</script>");
    }
}
