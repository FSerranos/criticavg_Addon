/*
 * Critic AVG for Jellyfin: shows the Critic AVG score on the film page.
 * Loaded by the plugin into Jellyfin web (and the apps built on it: Android phone app, Jellyfin Media Player).
 *
 *  Beside → [★ 6.8] [🍅 100] [Critic AVG 7.4]   star and tomato keep the film's own values
 *  Alone  → [Critic AVG 7.4]                      star and tomato hidden
 */
(function () {
    'use strict';
    if (window.__criticAvgLoaded) { return; }
    window.__criticAvgLoaded = true;

    var cache = {};      // itemId -> data, or null when the film has no score
    var pending = {};
    var timer = null;

    var css = ''
        + '.criticAvgBadge{display:inline-flex!important;align-items:center;gap:.35em;padding:.1em .55em .1em .2em;'
        + 'border-radius:1em;background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.18);'
        + 'font-weight:600;line-height:1.5;white-space:nowrap;cursor:default}'
        + '.criticAvgBadge .criticAvgMark{display:inline-flex;align-items:center;justify-content:center;'
        + 'min-width:1.5em;height:1.5em;border-radius:50%;font-size:.7em;font-weight:800;letter-spacing:-.02em;color:#111}'
        + '.criticAvgBadge .criticAvgLabel{font-size:.75em;font-weight:500;opacity:.7;text-transform:uppercase;letter-spacing:.04em}'
        + '.criticAvgHigh .criticAvgMark{background:#3ddc84}'
        + '.criticAvgMid .criticAvgMark{background:#f5c518}'
        + '.criticAvgLow .criticAvgMark{background:#ff5a4f}'
        + '.criticAvgHidden{display:none!important}';
    var style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);

    function api() { return window.ApiClient; }

    /** The film id from the address: #/details?id=… */
    function currentItemId() {
        var m = /[?&]id=([0-9a-fA-F-]{32,36})/.exec(window.location.hash || window.location.search || '');
        return m ? m[1].replace(/-/g, '').toLowerCase() : null;
    }

    /** The ratings row of the film page that is on screen. */
    function findRow() {
        var rows = document.querySelectorAll('.itemMiscInfo-primary, .itemMiscInfo');
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i];
            if (r.offsetParent !== null && !r.closest('.hide') && r.closest('#itemDetailPage, .itemDetailPage, .detailPagePrimaryContainer, .detailPageContent')) {
                return r;
            }
        }
        return null;
    }

    function load(id) {
        if (cache.hasOwnProperty(id) || pending[id] || !api()) { return; }
        pending[id] = true;
        var client = api();
        client.ajax({ type: 'GET', url: client.getUrl('CriticAvg/Items/' + id), dataType: 'json' })
            .then(function (d) { cache[id] = d && typeof d.score === 'number' ? d : null; })
            .catch(function () { cache[id] = null; })
            .then(function () { delete pending[id]; schedule(); });
    }

    function fmt10(v) { return (Math.round(v * 10) / 10).toFixed(1); }

    /** Puts the film's own value back on the star / tomato when the field holds Critic AVG (for sorting). */
    function restoreStar(row, info) {
        var star = row.querySelector('.starRatingContainer');
        if (!star || !info || !info.holdsScore) { return; }
        if (info.original == null) { star.classList.add('criticAvgHidden'); return; }
        var nodes = star.childNodes;
        for (var i = nodes.length - 1; i >= 0; i--) {
            if (nodes[i].nodeType === 3 && nodes[i].nodeValue.trim()) { nodes[i].nodeValue = fmt10(info.original); return; }
        }
        star.appendChild(document.createTextNode(fmt10(info.original)));
    }

    function restoreTomato(row, info) {
        var tomato = row.querySelector('.mediaInfoCriticRating');
        if (!tomato || !info || !info.holdsScore) { return; }
        if (info.original == null) { tomato.classList.add('criticAvgHidden'); return; }
        var v = Math.round(info.original);
        tomato.textContent = String(v);
        tomato.classList.toggle('mediaInfoCriticRatingFresh', v >= 60);
        tomato.classList.toggle('mediaInfoCriticRatingRotten', v < 60);
    }

    function badge(d) {
        var el = document.createElement('div');
        var s = d.score;
        el.className = 'mediaInfoItem criticAvgBadge ' + (s >= 7 ? 'criticAvgHigh' : s >= 5 ? 'criticAvgMid' : 'criticAvgLow');
        el.title = 'Critic AVG · ' + d.preset;
        el.innerHTML = '<span class="criticAvgMark">AVG</span><span class="criticAvgValue"></span><span class="criticAvgLabel">Critic AVG</span>';
        el.querySelector('.criticAvgValue').textContent = fmt10(s);
        return el;
    }

    function render() {
        timer = null;
        var id = currentItemId();
        if (!id) { return; }
        var row = findRow();
        if (!row) { return; }
        if (!cache.hasOwnProperty(id)) { load(id); return; }
        var d = cache[id];
        if (!d) { return; }
        if (row.getAttribute('data-critic-avg') === id && row.querySelector('.criticAvgBadge')) { return; }

        // Jellyfin re-uses the page: clear what a previous film left.
        var old = row.querySelectorAll('.criticAvgBadge');
        for (var i = 0; i < old.length; i++) { old[i].remove(); }
        var hidden = row.querySelectorAll('.criticAvgHidden');
        for (var j = 0; j < hidden.length; j++) { hidden[j].classList.remove('criticAvgHidden'); }

        var star = row.querySelector('.starRatingContainer');
        var tomato = row.querySelector('.mediaInfoCriticRating');

        if (d.displayMode === 'Alone') {
            if (star) { star.classList.add('criticAvgHidden'); }
            if (tomato) { tomato.classList.add('criticAvgHidden'); }
        } else {
            restoreStar(row, d.communityRating);
            restoreTomato(row, d.criticRating);
        }

        var b = badge(d);
        var after = tomato || star;
        if (after && after.parentNode === row) { row.insertBefore(b, after.nextSibling); } else { row.appendChild(b); }
        row.setAttribute('data-critic-avg', id);
    }

    function schedule() {
        if (!timer) { timer = setTimeout(render, 150); }
    }

    new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
    window.addEventListener('hashchange', schedule);
    window.addEventListener('popstate', schedule);
    schedule();
})();
