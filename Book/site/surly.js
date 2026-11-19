// Copyright (c) 2026 Stewart Rogers. Apache License 2.0.
// The site's one script. Every page works without it (folded sections open on a click);
// it adds: opening the folds around a linked target, unfolding everything to print,
// "Expand all" / "Collapse all", and the recipe search on the contents page.
(function () {
  "use strict";

  // Open every <details> that holds the element a #link points at, then show it.
  function reveal(id) {
    if (!id) return;
    var target = document.getElementById(decodeURIComponent(id));
    if (!target) return;
    for (var node = target; node; node = node.parentElement) {
      if (node.tagName === "DETAILS") node.open = true;
    }
    target.scrollIntoView();
  }
  window.addEventListener("hashchange", function () { reveal(location.hash.slice(1)); });
  document.addEventListener("click", function (e) {
    var a = e.target.closest && e.target.closest('a[href^="#"]');
    if (a) setTimeout(function () { reveal(a.getAttribute("href").slice(1)); }, 0);
  });

  // Print everything, then put the folds back as they were.
  var closed = [];
  window.addEventListener("beforeprint", function () {
    closed = Array.prototype.filter.call(document.querySelectorAll("details"), function (d) { return !d.open; });
    closed.forEach(function (d) { d.open = true; });
  });
  window.addEventListener("afterprint", function () { closed.forEach(function (d) { d.open = false; }); });

  // Expand all / Collapse all.
  document.addEventListener("click", function (e) {
    var b = e.target.closest && e.target.closest("[data-fold]");
    if (!b) return;
    var open = b.getAttribute("data-fold") === "open";
    document.querySelectorAll("details.fold").forEach(function (d) { d.open = open; });
  });

  // Search: filter the contents page's recipes by every word typed, in their title, Problem,
  // protocols, options and key. Matches stay visible with their part and chapter opened.
  function search(box) {
    var words = box.value.toLowerCase().split(/\s+/).filter(Boolean);
    var items = document.querySelectorAll("li[data-search]");
    var shown = 0;
    items.forEach(function (li) {
      var hay = li.getAttribute("data-search");
      var hit = words.every(function (w) { return hay.indexOf(w) >= 0; });
      li.hidden = words.length > 0 && !hit;
      if (!li.hidden) shown++;
    });
    document.querySelectorAll("details.fold").forEach(function (d) {
      var any = d.querySelector("li[data-search]:not([hidden])");
      d.hidden = words.length > 0 && !any;
      if (words.length) d.open = !!any;
    });
    var count = document.getElementById("search-count");
    if (count) count.textContent = words.length ? shown + " of " + items.length + " recipes match" : items.length + " recipes";
  }

  document.addEventListener("DOMContentLoaded", function () {
    var box = document.getElementById("search");
    if (box) {
      var q = new URLSearchParams(location.search).get("q");
      if (q) box.value = q;
      box.addEventListener("input", function () { search(box); });
      search(box);
    }
    document.querySelectorAll("form.search input[name=q]").forEach(function (input) {
      var q = new URLSearchParams(location.search).get("q");
      if (q && !input.value) input.value = q;
    });
    reveal(location.hash.slice(1));
  });
})();
