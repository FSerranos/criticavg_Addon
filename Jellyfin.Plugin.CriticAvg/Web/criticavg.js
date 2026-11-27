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

    // Critic AVG logo (brand gold), shown at text size.
    var LOGO = '<svg class="criticAvgLogo" viewBox="0 0 189.78 204.31" aria-hidden="true" fill="#ce9109"><path d="M113.89,85.39l57.49-.08c.71-.05,1,.1.84.46l-46.58,33.86c-.66.48-1.25,1.02-.94,1.96l18.25,55.07-46.86-33.64c-.74-.7-1.47-.66-2.29-.07l-46.92,33.7,18.03-54.66c.35-1.06.09-1.81-.8-2.41l-46.44-33.76c-.3-.39-.06-.57.72-.52l57.4.08c.57-.48,1.21-1.07,1.5-1.94l17.58-52.31,17.62,52.2c.33.98.9,1.3,1.38,2.07Z"/><path d="M75.8,85.38l-57.24.06c-.54,0-.68,0-.88.38-.28-.2-.67-.35-.64-.66l58.76.22Z"/><path d="M170.2,85.19c.76-.19,1.57-.16,2.35-.12.22.01.27.14.18.36-.21.06-.37.25-.5.34-.23-.19-.41-.32-.85-.32l-57.48-.05,56.3-.21Z"/><path d="M172.72,85.42c-.08-.16-.26-.25-.52-.25h-2c.57-.38,1.41-.25,2.29-.2.37-.28.71.33.24.45Z"/><path d="M25.99,138.41c-4.35,5.5-4.03,13.14.54,18.32,4.03,4.56,10.17,6.04,16.11,4.16l-7.29,21.93C13.17,164.95.04,138.61,0,110.22c-.01-8.12,1.23-15.93,3.25-23.83l14.95,10.84c-4.99,2.67-7.82,7.4-7.91,12.62s2.61,10.11,7.45,12.87c4.09,2.33,9.19,2.53,13.72.24,3.71-1.87,6.79-5.67,7.75-10.38l15.68,11.27-5.06,15.32c-2.88-4.05-7.07-6.33-11.63-6.49-4.76-.16-9.11,1.83-12.18,5.71Z"/><path d="M70.22,76.08h-17.68c3.55-5.96,2.62-13.06-2.04-17.79s-11.79-5.7-17.62-2.36c-7.15,4.04-9.47,13.16-5.2,20.16H6.52c7.07-17.65,19.13-32.53,34.76-42.98,8.94-5.96,18.66-10.29,29.12-12.75,6.37-1.66,12.63-2.42,19.38-2.57l-6.11,18.21c-4.27-4.19-10.37-5.52-15.99-3.3-4.93,1.95-8.82,6.83-9.11,12.51-.47,9.2,7.58,16.31,16.86,14.91l-1.74,5.63-3.47,10.31Z"/><path d="M172.91,163.21c-5.25,7.45-11.43,14.02-18.53,19.65l-3.92-12.03c15.24-13.19,26.5-30.33,32.58-49.48,3.26-10.25,4.55-20.68,4.33-31.52,6.05,25.44.53,52.02-14.46,73.38Z"/><path d="M116.78,201.68c-14.38,3.44-28.3,3.49-42.51.25l-10.73-3.15c-6.66-2.55-12.9-5.56-18.96-9.46l10.28-7.32c8.14,4.37,16.82,7.5,26.01,9.12,18.56,3.24,37.41.08,53.99-9.17l10.36,7.37c-8.78,5.75-18.18,9.68-28.44,12.35Z"/><polygon points="166.35 23.17 145.77 7.04 174.75 0 182.71 19.22 166.35 23.17"/><polygon points="139.15 76.14 123.37 76.11 140.19 56 155.95 56.04 139.15 76.14"/><polygon points="170.4 76.15 154.64 76.12 171.54 56.01 187.34 56.02 170.4 76.15"/><polygon points="151.03 26.64 135.87 30.35 115.38 14.31 130.6 10.54 151.03 26.64"/><polygon points="120.78 33.87 107.59 37.09 101.47 18.83 120.78 33.87"/><polygon points="124.43 56.07 116.32 65.95 112.93 56.01 124.43 56.07"/><polygon points="188.03 76.06 180.39 76.06 187.8 67.17 188.03 76.06"/></svg>';

    var css = ''
        + '.criticAvgBadge{display:inline-flex!important;align-items:center;gap:.4em;white-space:nowrap;cursor:default;font-weight:600}'
        + '.criticAvgBadge .criticAvgLogo{width:auto;height:1.25em;flex:none}'
        + '.criticAvgBadge .criticAvgValue{padding:0 .45em;border-radius:.3em;color:#111;font-weight:700;line-height:1.45}'
        + '.criticAvgHigh .criticAvgValue{background:#3ddc84}'
        + '.criticAvgMid .criticAvgValue{background:#f5c518}'
        + '.criticAvgLow .criticAvgValue{background:#ff5a4f}'
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
        el.innerHTML = LOGO + '<span class="criticAvgValue"></span>';
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
