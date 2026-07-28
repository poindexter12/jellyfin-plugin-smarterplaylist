/* Smarter Playlist docs.
   Everything here is additive: with JS off, every recipe still reads, the JSON is still
   highlighted (Rouge does that server-side) and every anchor link still works. */
(function () {
  var root = document.documentElement;
  root.classList.add('js');

  // 1. Copy buttons ------------------------------------------------------
  // The JSON blocks are what people came for, and selecting 20 lines by hand is the
  // friction this removes.
  document.querySelectorAll('main .highlight').forEach(function (hl) {
    var fig = document.createElement('figure');
    fig.className = 'code';
    hl.parentNode.insertBefore(fig, hl);
    fig.appendChild(hl);

    var pre = fig.querySelector('pre');
    if (!pre) return;
    var text = pre.textContent.replace(/\n$/, '');

    // A complete definition gets a label that says so; the "Combining these" fragment
    // does not, because pasting it whole would not work.
    var label = 'Copy';
    try {
      var parsed = JSON.parse(text);
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed) && 'Name' in parsed) {
        label = 'Copy definition';
      }
    } catch (e) { /* fragment or non-JSON: keep the generic label */ }

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'copy';
    btn.textContent = label;
    btn.setAttribute('aria-live', 'polite');

    btn.addEventListener('click', function () {
      var done = function () {
        btn.textContent = 'Copied';
        btn.dataset.state = 'done';
        setTimeout(function () {
          btn.textContent = label;
          delete btn.dataset.state;
        }, 1600);
      };
      // Clipboard access can be refused outright; selecting the text is the fallback
      // that still leaves the reader one keystroke from having it.
      var fail = function () {
        btn.textContent = 'Press ⌘C';
        var r = document.createRange();
        r.selectNodeContents(pre);
        var s = window.getSelection();
        s.removeAllRanges();
        s.addRange(r);
        setTimeout(function () { btn.textContent = label; }, 2400);
      };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(done, fail);
      } else {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.style.cssText = 'position:absolute;left:-9999px';
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy') ? done() : fail(); } catch (e) { fail(); }
        document.body.removeChild(ta);
      }
    });

    fig.appendChild(btn);
  });

  // 2. Heading anchors ---------------------------------------------------
  var heads = Array.prototype.slice.call(document.querySelectorAll('main h2[id]'));
  heads.forEach(function (h) {
    var a = document.createElement('a');
    a.className = 'anchor';
    a.href = '#' + h.id;
    a.textContent = '#';
    a.setAttribute('aria-label', 'Link to ' + h.textContent);
    h.appendChild(a);
  });

  // 3. Sidebar TOC + scroll-spy -----------------------------------------
  // Decided from the content, not configuration: the home page has two headings and gets
  // no rail, the recipes page has eleven and does.
  var rail = document.querySelector('.toc-rail');
  if (rail && heads.length > 2) {
    var nav = document.createElement('nav');
    nav.setAttribute('aria-label', 'On this page');
    var ul = document.createElement('ul');
    var links = {};
    heads.forEach(function (h) {
      var li = document.createElement('li');
      var a = document.createElement('a');
      a.href = '#' + h.id;
      a.textContent = h.firstChild ? h.firstChild.textContent.trim() : h.id;
      li.appendChild(a);
      ul.appendChild(li);
      links[h.id] = a;
    });
    nav.appendChild(ul);
    rail.appendChild(nav);
    rail.hidden = false;
    root.classList.add('html-has-toc');

    if ('IntersectionObserver' in window) {
      var seen = {};
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) { seen[e.target.id] = e.isIntersecting; });
        var active = null;
        heads.forEach(function (h) { if (!active && seen[h.id]) active = h.id; });
        heads.forEach(function (h) {
          if (h.id === active) links[h.id].setAttribute('aria-current', 'true');
          else links[h.id].removeAttribute('aria-current');
        });
      }, { rootMargin: '-72px 0px -70% 0px' });
      heads.forEach(function (h) { io.observe(h); });
    }
  }

  // 4. Back-to-top visibility -------------------------------------------
  var tick = false;
  window.addEventListener('scroll', function () {
    if (tick) return;
    tick = true;
    requestAnimationFrame(function () {
      root.classList.toggle('is-scrolled', window.scrollY > 600);
      tick = false;
    });
  }, { passive: true });
})();
