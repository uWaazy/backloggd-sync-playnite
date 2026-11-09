namespace BackloggdSync
{
    /// <summary>
    /// JavaScript injetado na janela do site durante a captura. Registra (mascarando segredos) as requisições do site,
    /// envios de formulário, cliques e navegação, e desenha a barra de instruções. O registro fica no sessionStorage
    /// para sobreviver a trocas de página. Não usa aspas duplas (o texto abaixo é um literal C#).
    /// </summary>
    public static class CaptureScript
    {
        public const string Hook = @"
(function(){
  if (!window.__blHooked) {
    window.__blHooked = true;
    var KEY = '__bl_log';
    var SENS = /token|csrf|password|passwd|secret|auth|session|cookie|signature|api[_-]?key/i;
    function load(){ try { return JSON.parse(sessionStorage.getItem(KEY) || '[]'); } catch(e) { return []; } }
    function save(l){
      try {
        if (l.length > 300) { l = l.slice(l.length - 300); }
        var s = JSON.stringify(l);
        while (s.length > 3000000 && l.length > 10) { l = l.slice(20); s = JSON.stringify(l); }
        sessionStorage.setItem(KEY, s);
      } catch(e) {}
    }
    function rec(e){ try { e.t = new Date().toISOString(); var l = load(); l.push(e); save(l); } catch(x) {} }
    function cut(s, n){ s = (s == null) ? '' : String(s); return s.length > n ? s.substring(0, n) + '...[cut]' : s; }
    function walk(o){ if (o && typeof o === 'object') { for (var k in o) { if (SENS.test(k)) { o[k] = '***'; } else { walk(o[k]); } } } return o; }
    function mask(s){
      if (s == null) return '';
      if (typeof s !== 'string') {
        try {
          if (typeof FormData !== 'undefined' && s instanceof FormData) {
            var parts = [];
            s.forEach(function(v, k){ parts.push(encodeURIComponent(k) + '=' + (SENS.test(k) ? '***' : (typeof v === 'string' ? encodeURIComponent(v) : '[file]'))); });
            return parts.join('&');
          }
          if (typeof URLSearchParams !== 'undefined' && s instanceof URLSearchParams) { s = s.toString(); }
          else { return '[non-text body]'; }
        } catch(x) { return '[body]'; }
      }
      var t = s.trim();
      if (t.charAt(0) === '{' || t.charAt(0) === '[') { try { return JSON.stringify(walk(JSON.parse(t))); } catch(e) {} }
      if (t.indexOf('=') >= 0 && t.indexOf('<') < 0) {
        return t.split('&').map(function(p){
          var i = p.indexOf('='); if (i < 0) return p;
          var k = p.substring(0, i), kd = k;
          try { kd = decodeURIComponent(k.replace(/\+/g, ' ')); } catch(e) {}
          return SENS.test(kd) ? (k + '=***') : p;
        }).join('&');
      }
      return t;
    }
    function maskResp(t){
      t = String(t || '');
      t = t.replace(/(authenticity_token[^>]{0,80}?value=[\x22\x27])[^\x22\x27]*/gi, '$1***');
      t = t.replace(/(csrf-token[\x22\x27]\s+content=[\x22\x27])[^\x22\x27]*/gi, '$1***');
      t = t.replace(/(\x22[^\x22]*(?:token|csrf|password|secret|session|cookie)[^\x22]*\x22\s*:\s*)\x22[^\x22]*\x22/gi, '$1\x22***\x22');
      return t;
    }
    var HOST = /(^|\.)backloggd\.com$/i;
    var STATIC = /\.(js|css|png|jpe?g|gif|svg|webp|avif|woff2?|ttf|ico|map|mp4|webm)(\?|#|$)/i;
    function keep(u){ try { var a = document.createElement('a'); a.href = u; if (!HOST.test(a.hostname)) return false; return !STATIC.test(a.pathname); } catch(e) { return false; } }
    function hnames(h){
      var r = [];
      try {
        if (!h) return r;
        if (typeof Headers !== 'undefined' && h instanceof Headers) { h.forEach(function(v, k){ r.push(k); }); }
        else { for (var k in h) { r.push(k); } }
      } catch(e) {}
      return r;
    }

    var of = window.fetch;
    if (of) {
      window.fetch = function(input, init){
        var url = (typeof input === 'string') ? input : ((input && input.url) || '');
        var method = (init && init.method) || (input && input.method) || 'GET';
        var body = (init && init.body != null) ? init.body : '';
        var hn = hnames(init && init.headers);
        var p = of.apply(this, arguments);
        if (keep(url)) {
          var sent = mask(body);
          p.then(function(r){
            try {
              var ct = r.headers.get('content-type') || '';
              r.clone().text().then(function(tx){
                rec({k:'fetch', u:cut(url, 500), m:String(method).toUpperCase(), hn:hn, b:cut(sent, 3000), s:r.status, ct:ct, r:cut(maskResp(tx), ct.indexOf('html') >= 0 ? 1200 : 3000)});
              });
            } catch(x) {}
          }).catch(function(){});
        }
        return p;
      };
    }

    var xo = XMLHttpRequest.prototype.open, xs = XMLHttpRequest.prototype.send, xh = XMLHttpRequest.prototype.setRequestHeader;
    XMLHttpRequest.prototype.open = function(m, u){ this.__bl = {m:String(m).toUpperCase(), u:String(u), hn:[], h:{}}; return xo.apply(this, arguments); };
    XMLHttpRequest.prototype.setRequestHeader = function(k, v){
      try { if (this.__bl) { this.__bl.hn.push(k); if (/^(content-type|accept|x-requested-with)$/i.test(k)) { this.__bl.h[k] = v; } } } catch(e) {}
      return xh.apply(this, arguments);
    };
    XMLHttpRequest.prototype.send = function(b){
      var self = this;
      try {
        if (this.__bl && keep(this.__bl.u)) {
          var sent = mask(b);
          this.addEventListener('loadend', function(){
            try {
              var ct = self.getResponseHeader('content-type') || '';
              var rt = ''; try { rt = self.responseText || ''; } catch(e) {}
              rec({k:'xhr', u:cut(self.__bl.u, 500), m:self.__bl.m, hn:self.__bl.hn, h:self.__bl.h, b:cut(sent, 3000), s:self.status, ct:ct, r:cut(maskResp(rt), ct.indexOf('html') >= 0 ? 1200 : 3000)});
            } catch(e) {}
          });
        }
      } catch(x) {}
      return xs.apply(this, arguments);
    };

    function ser(f){
      try {
        var parts = [], els = f.elements;
        for (var i = 0; i < els.length; i++) {
          var e = els[i]; if (!e.name) continue;
          if ((e.type === 'checkbox' || e.type === 'radio') && !e.checked) continue;
          var v = (e.type === 'password' || SENS.test(e.name)) ? '***' : e.value;
          parts.push(e.name + '=' + cut(v, 120));
        }
        return parts.join('&');
      } catch(x) { return ''; }
    }
    document.addEventListener('submit', function(ev){
      try { var f = ev.target; rec({k:'form', u:cut(f.action, 500), m:(f.method || 'get').toUpperCase(), id:f.id || '', b:cut(ser(f), 3000)}); } catch(x) {}
    }, true);
    document.addEventListener('click', function(ev){
      try {
        var el = (ev.target && ev.target.closest) ? ev.target.closest('a,button,input,select,[data-url],[data-remote],[data-method],[role=button]') : null;
        if (!el || el.type === 'password') return;
        var d = {}, at = el.attributes;
        for (var i = 0; i < at.length; i++) {
          var n = at[i].name;
          if (n.indexOf('data-') === 0 || n === 'href' || n === 'id' || n === 'name' || n === 'type' || n === 'value' || n === 'aria-label' || n === 'title') { d[n] = cut(at[i].value, 150); }
        }
        rec({k:'click', tag:el.tagName, txt:cut((el.innerText || el.value || '').replace(/\s+/g, ' ').trim(), 60), cls:cut(el.className, 100), a:d});
      } catch(x) {}
    }, true);

    function nav(){ try { rec({k:'nav', u:cut(location.href, 500), title:cut(document.title, 100)}); } catch(e) {} }
    nav();
    try { var ps = history.pushState; history.pushState = function(){ var r = ps.apply(this, arguments); nav(); return r; }; } catch(e) {}
    document.addEventListener('turbolinks:load', nav);
    document.addEventListener('turbo:load', nav);

    window.__blBannerUpdate = function(){
      try {
        var cfg = window.__blBanner; if (!cfg) return;
        var b = document.getElementById('__bl_banner');
        if (!b) {
          b = document.createElement('div'); b.id = '__bl_banner';
          b.style.cssText = 'position:fixed;left:50%;bottom:12px;transform:translateX(-50%);max-width:780px;width:92%;z-index:2147483647;background:#0f172a;color:#f8fafc;font:13px/1.45 Segoe UI,Arial,sans-serif;padding:10px 14px;border-radius:10px;border:1px solid #38bdf8;box-shadow:0 6px 24px rgba(0,0,0,.5);cursor:pointer';
          b.onclick = function(){ var c = b.querySelector('.__bl_body'); if (c) { c.style.display = (c.style.display === 'none') ? 'block' : 'none'; } };
          (document.body || document.documentElement).appendChild(b);
        }
        var key = cfg.title + '|' + cfg.body;
        if (b.getAttribute('data-k') !== key) {
          b.setAttribute('data-k', key);
          while (b.firstChild) { b.removeChild(b.firstChild); }
          var h = document.createElement('div'); h.style.cssText = 'font-weight:700;color:#38bdf8;margin-bottom:4px'; h.textContent = cfg.title; b.appendChild(h);
          var c = document.createElement('div'); c.className = '__bl_body'; c.textContent = cfg.body; b.appendChild(c);
          var f = document.createElement('div'); f.style.cssText = 'margin-top:6px;color:#94a3b8;font-size:11px'; f.textContent = cfg.hint; c.appendChild(f);
        }
      } catch(e) {}
    };

    window.__blSnapshot = function(){
      try {
        var o = {url:cut(location.href, 300), title:cut(document.title, 100), forms:[], dataEls:[], meta:[]};
        var fs = document.forms;
        for (var i = 0; i < fs.length && i < 30; i++) {
          var f = fs[i], fl = [];
          for (var j = 0; j < f.elements.length && j < 40; j++) {
            var e = f.elements[j]; if (!e.name) continue;
            var item = {n:e.name, t:e.type, v:(e.type === 'password' || SENS.test(e.name)) ? '***' : cut(e.value, 60)};
            if (e.tagName === 'SELECT') { item.o = Array.prototype.slice.call(e.options, 0, 12).map(function(x){ return x.value + ':' + cut(x.text, 24); }); }
            fl.push(item);
          }
          o.forms.push({id:f.id || '', cls:cut(f.className, 80), action:cut(f.action, 300), method:(f.method || 'get').toUpperCase(), fields:fl});
        }
        var els = document.querySelectorAll('[data-url],[data-remote],[data-method],[data-action],[data-game-id],[data-id],[data-path]');
        for (var k = 0; k < els.length && k < 60; k++) {
          var el = els[k], d = {tag:el.tagName, id:el.id || '', cls:cut(el.className, 60)};
          for (var a = 0; a < el.attributes.length; a++) { var an = el.attributes[a].name; if (an.indexOf('data-') === 0) { d[an] = cut(el.attributes[a].value, 120); } }
          o.dataEls.push(d);
        }
        var ms = document.querySelectorAll('meta[name]');
        for (var m = 0; m < ms.length && m < 30; m++) { var n = ms[m].getAttribute('name'); if (/csrf/i.test(n)) { o.meta.push({name:n, content:'***'}); } }
        return JSON.stringify(o);
      } catch(x) { return ''; }
    };
  }
  if (window.__blBannerUpdate) { window.__blBannerUpdate(); }
})();";
    }
}
