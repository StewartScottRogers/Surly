#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Build the website: the cover and the whole book in one look (site/surly.css).

Reads book.yml (identity), site/template.html (the cover), site/page.html (every
book page), generated/index.json and the pages build.py rendered, and writes:
  out/site/index.html          the cover, with the book's live status and its parts
  out/site/contents.html       every part, chapter and recipe, with its Problem and status
  out/site/recipes/<key>.html  one page per recipe, with breadcrumbs and previous/next
  out/site/book.html           the whole book on one page
  dist/                        the pitch for handing out: pitch.html, pitch.pdf, pitch.md, pitch.txt, cover.png, cover.svg
Run build.py first. The cover PNG and the pitch PDF need Node with playwright; without
it they are skipped with a warning, and the site is complete without them.
"""
import datetime, html, json, re, shutil, subprocess, sys
from pathlib import Path
import yaml
from recipes import BOOK, recipes

SITE, DIST, OUT = BOOK / "site", BOOK / "dist", BOOK / "out" / "site"
REPO = "https://github.com/StewartScottRogers/Surly"
CURL = "https://github.com/StewartScottRogers/Curl"
SURL = "https://github.com/StewartScottRogers/Surl"




def unlink_examples(body):
    """Asciidoctor links every URL it finds, so an example such as ftp://127.0.0.1:8080/ or
    file:/// becomes a link to the reader's own machine. Keep the text, drop the link, for any
    scheme but http, https and mailto, and for any address on the loopback."""
    def plain(m):
        href = m.group(1)
        web = href.startswith(("http://", "https://", "mailto:")) or not re.match(r"[a-z][a-z0-9+.-]*:", href)
        local = re.match(r"[a-z][a-z0-9+.-]*://(127\.0\.0\.1|localhost|\[::1\])", href)
        return m.group(0) if web and not local else m.group(2)
    return re.sub(r'<a href="([^"]+)"[^>]*>(.*?)</a>', plain, body, flags=re.S)


def rendered_content(path):
    """The body Asciidoctor wrote for a page: everything inside <div id="content">."""
    text = path.read_text(encoding="utf-8")
    m = re.search(r'<div id="content">(.*)</div>\s*<div id="footer">', text, re.S)
    return unlink_examples(m.group(1) if m else text)


def part_and_chapter_titles():
    """{folder name: (title, description)} from each part.adoc and chapter.adoc."""
    titles = {}
    adocs = list((BOOK / "parts").glob("*/part.adoc")) + list((BOOK / "parts").glob("*/chapters/*/chapter.adoc"))
    for adoc in adocs:
        lines = [l for l in adoc.read_text(encoding="utf-8").splitlines() if l.strip() and not l.startswith("//")]
        title = re.sub(r"^=+\s*", "", lines[0]) if lines else adoc.parent.name
        titles[adoc.parent.name] = (title, " ".join(lines[1:]))
    return titles


def inline(text):
    """Escape prose for HTML and turn `code` into <code>, as the book's sentences use it."""
    return re.sub(r"`([^`]+)`", r"<code>\1</code>", html.escape(text))


def cited_references_only(body):
    """A one-recipe page carries the whole bibliography; keep only the entries it cites."""
    start = body.find('id="_references"')
    if start < 0:
        return body
    cited = set(re.findall(r'href="#([a-z0-9-]+)"', body[:start]))

    def keep(m):
        anchor = re.search(r'<a id="([a-z0-9-]+)"', m.group(0))
        return m.group(0) if anchor and anchor.group(1) in cited else ""
    return body[:start] + re.sub(r"<li>.*?</li>", keep, body[start:], flags=re.S)


def page(values, page_title, description, body, root="", crumbs="", prevnext="", current=""):
    """One book page in the site's frame (site/page.html)."""
    out = (SITE / "page.html").read_text(encoding="utf-8")
    fill = {**values, "page_title": html.escape(page_title), "description": html.escape(description), "body": body,
            "root": root, "crumbs": crumbs, "prevnext": prevnext,
            "current_contents": ' aria-current="page"' if current == "contents" else "",
            "current_book": ' aria-current="page"' if current == "book" else ""}
    for k, v in fill.items():
        out = out.replace("{{" + k + "}}", str(v))
    return out


def badges(r):
    b = ""
    if r["surface"] != "console":
        b += '<span class="badge">' + ("C#" if r["surface"] == "csharp" else "MCP") + "</span>"
    status = r.get("status", "draft")
    return b + f'<span class="badge {status}">{status}</span>'


def fold_claims_and_references(content):
    """On a recipe page, fold the checked-claims table (with the servers and programs it lists)
    and the references: Problem, Solution and Discussion stay open."""
    caption = content.find("Checked claims</caption>")
    if caption >= 0:
        start = content.rfind("<table", 0, caption)
        see_also = content.find('<div class="sect2">', caption)
        end = content.rfind("</div>", start, see_also if see_also >= 0 else len(content))
        rows = content.count("<tr>", start, end) - 1
        content = (content[:start] + f'<details class="fold claims"><summary>Checked claims '
                   f'<span class="count">{rows}</span></summary>' + content[start:end] + "</details>" + content[end:])
    head = content.find('<h2 id="_references">')
    if head >= 0:
        close = content.find("</h2>", head) + len("</h2>")
        refs = content.count("<li>", close)
        last = content.rstrip().rfind("</div>")
        content = (content[:head] + f'<details class="fold refs" id="_references"><summary>References '
                   f'<span class="count">{refs}</span></summary>' + content[close:last] + "</details>" + content[last:])
    return content


def search_text(r):
    """What the contents page's search looks in: title, Problem, protocols, options, types, tools, key."""
    words = [r["title"], r["problem"], r["key"], r["surface"], " ".join(r["protocols"]),
             " ".join(r.get("surl-options", [])), " ".join(r.get("curl-options", [])),
             " ".join(r.get("api-types", [])), " ".join(r.get("mcp-tools", []))]
    return html.escape(" ".join(words).lower(), quote=True)


FOLDBAR = ('<div class="foldbar"><button type="button" data-fold="open">Expand all</button>'
           '<button type="button" data-fold="close">Collapse all</button>{extra}</div>')


def write_book_pages(values, index, titles):
    """contents.html, recipes/<key>.html and book.html, all in the site's look, folded."""
    keys = {r["key"] for r in index}
    (OUT / "recipes").mkdir(parents=True, exist_ok=True)
    part_numbers = {p: i + 1 for i, p in enumerate(dict.fromkeys(r["part"] for r in index))}
    parts = list(dict.fromkeys(r["part"] for r in index))

    def chapters_of(part):
        return list(dict.fromkeys(r["chapter"] for r in index if r["part"] == part))

    # Contents: folded parts and chapters, and a search over every recipe.
    body = ["<h1>Contents</h1>",
            '<input id="search" type="search" placeholder="Search: a word, a protocol, an option such as --user" '
            'aria-label="Search recipes" autocomplete="off">',
            FOLDBAR.format(extra='<span id="search-count"></span>')]
    for part in parts:
        n = part_numbers[part]
        title, about = titles.get(part, (part, ""))
        in_part = [r for r in index if r["part"] == part]
        body.append(f'<details class="fold part" id="part-{n}"><summary><h2>Part {n}. {html.escape(title)}</h2>'
                    f'<span class="count">{len(in_part)} recipes</span></summary><p>{inline(about)}</p>')
        for chapter in chapters_of(part):
            in_chapter = [r for r in in_part if r["chapter"] == chapter]
            body.append(f'<details class="fold chapter"><summary><h3>{html.escape(titles.get(chapter, (chapter, ""))[0])}</h3>'
                        f'<span class="count">{len(in_chapter)}</span></summary><ol>')
            for r in in_chapter:
                body.append(f'<li data-search="{search_text(r)}"><a href="recipes/{r["key"]}.html">{html.escape(r["title"])}</a>'
                            f'{badges(r)}<p>{inline(r["problem"])}</p></li>')
            body.append("</ol></details>")
        body.append("</details>")
    (OUT / "contents.html").write_text(
        page(values, "Contents", "Every recipe in the book, by part and chapter, with a search.",
             '<div class="contents">' + "\n".join(body) + "</div>", current="contents"), encoding="utf-8")

    # One page per recipe, its claims and references folded.
    bodies = {}
    for i, r in enumerate(index):
        content = rendered_content(BOOK / r["rendered"])
        bodies[r["key"]] = content
        content = cited_references_only(content)
        content = re.sub(r'href="#([a-z0-9-]+)"',
                         lambda m: f'href="{m.group(1)}.html"' if m.group(1) in keys else m.group(0), content)
        content = fold_claims_and_references(content)
        n = part_numbers[r["part"]]
        part_title = titles.get(r["part"], (r["part"], ""))[0]
        chapter_title = titles.get(r["chapter"], (r["chapter"], ""))[0]
        crumbs = (f'<p class="crumbs"><a href="../contents.html">Contents</a> › '
                  f'<a href="../contents.html#part-{n}">Part {n}. {html.escape(part_title)}</a> › {html.escape(chapter_title)}</p>')
        links = []
        if i > 0:
            prev = index[i - 1]
            links.append(f'<a class="prev" href="{prev["key"]}.html"><span>Previous</span><b>{html.escape(prev["title"])}</b></a>')
        if i + 1 < len(index):
            nxt = index[i + 1]
            links.append(f'<a class="next" href="{nxt["key"]}.html"><span>Next</span><b>{html.escape(nxt["title"])}</b></a>')
        prevnext = '<nav class="prevnext" aria-label="Recipe">' + "".join(links) + "</nav>"
        (OUT / "recipes" / f'{r["key"]}.html').write_text(
            page(values, r["title"], r["problem"], content, root="../", crumbs=crumbs, prevnext=prevnext), encoding="utf-8")

    # The whole book on one page: front matter open, then parts > chapters > recipes folded, then
    # the appendix and the references once. A recipe's own heading becomes its fold's summary.
    whole = rendered_content(BOOK / "out" / "surly.html")
    first_part = whole.find('class="sect0"')
    front = whole[:whole.rfind("<h1", 0, first_part)] if first_part >= 0 else ""
    tail_at = whole.find('id="_recipes_by_protocol"')
    tail = whole[whole.rfind('<div class="sect1">', 0, tail_at):] if tail_at >= 0 else ""
    book = ["<h1>The whole book</h1>", FOLDBAR.format(extra=""), front]
    for part in parts:
        n = part_numbers[part]
        title, about = titles.get(part, (part, ""))
        book.append(f'<details class="fold part" id="part-{n}"><summary><h2>Part {n}. {html.escape(title)}</h2></summary>'
                    f'<p>{inline(about)}</p>')
        for chapter in chapters_of(part):
            chapter_title, chapter_about = titles.get(chapter, (chapter, ""))
            in_chapter = [r for r in index if r["chapter"] == chapter and r["part"] == part]
            book.append(f'<details class="fold chapter"><summary><h3>{html.escape(chapter_title)}</h3>'
                        f'<span class="count">{len(in_chapter)}</span></summary><p>{inline(chapter_about)}</p>')
            for r in in_chapter:
                content = bodies[r["key"]]
                if '<h2 id="_references">' in content:
                    content = content[:content.rfind('<div class="sect1">', 0, content.find('<h2 id="_references">'))]
                content = re.sub(r'<h2 id="' + re.escape(r["key"]) + r'">.*?</h2>', "", content, count=1, flags=re.S)
                book.append(f'<details class="fold recipe" id="{r["key"]}"><summary><span>{html.escape(r["title"])}'
                            f'{badges(r)}<span class="problem">{inline(r["problem"])}</span></span></summary>'
                            f'{content}<p><a href="recipes/{r["key"]}.html">This recipe on its own page</a></p></details>')
            book.append("</details>")
        book.append("</details>")
    if tail:
        book.append('<details class="fold part" id="appendix"><summary><h2>Recipes by protocol, and references</h2></summary>'
                    + tail + "</details>")
    (OUT / "book.html").write_text(
        page(values, "The whole book", values["pitch_one_line"], "\n".join(book), current="book"), encoding="utf-8")


def status_and_parts(index, titles):
    """The cover's live status (numbers counted from the build) and its list of parts."""
    total = len(index)
    claims = sum(len(r["claims"]) for r in index)
    protocols = len({p for r in index for p in r["protocols"]})
    surfaces = {s: sum(1 for r in index if r["surface"] == s) for s in ("console", "csharp", "mcp")}
    verified = sum(1 for r in index if r.get("status") == "verified")
    tiles = [(total, "recipes"), (claims, "checked claims"), (protocols, "protocols"),
             (surfaces["console"], "on the console"), (surfaces["csharp"], "in C#"), (surfaces["mcp"], "from an agent (MCP)")]
    status = '<div class="stats">' + "".join(f'<div class="stat"><b>{n}</b><span>{label}</span></div>'
                                             for n, label in tiles) + "</div>"
    if verified:
        state = f"{verified} of {total} are verified against released programs; the rest are drafts until Surl and Curl are released."
    else:
        state = (f"All {total} are drafts: every claim passes against the programs' current builds, and each recipe is "
                 "marked verified the day it passes against a release.")
    status += f'<p class="asof">{state} Counted from the build of {datetime.date.today().isoformat()}.</p>'
    parts = []
    for n, part in enumerate(dict.fromkeys(r["part"] for r in index), 1):
        title, about = titles.get(part, (part, ""))
        in_part = [r for r in index if r["part"] == part]
        chapters = len({r["chapter"] for r in in_part})
        parts.append(f'<li><a class="part" href="contents.html#part-{n}">Part {n}. {html.escape(title)}</a>'
                     f'<div class="meta">{len(in_part)} recipes in {chapters} chapters</div><p>{inline(about)}</p></li>')
    return status, '<ul class="parts">' + "".join(parts) + "</ul>"


def render_png_and_pdf():
    script = f"""
const {{chromium}} = require('playwright');
(async () => {{
  const b = await chromium.launch();
  let p = await b.newPage({{viewport: {{width: 1200, height: 1600}}, deviceScaleFactor: 1}});
  await p.goto('file://{OUT}/cover.svg');
  await p.screenshot({{path: '{OUT}/cover.png'}});
  p = await b.newPage({{viewport: {{width: 1000, height: 1400}}}});
  await p.goto('file://{OUT}/index.html');
  await p.emulateMedia({{media: 'print'}});
  await p.pdf({{path: '{DIST}/pitch.pdf', format: 'Letter', printBackground: true, margin: {{top: '0.6in', bottom: '0.6in', left: '0.6in', right: '0.6in'}}}});
  await b.close();
}})();
"""
    r = subprocess.run(["node", "-e", script], capture_output=True, text=True, cwd=BOOK)
    if r.returncode:
        if "Cannot find module 'playwright'" in r.stderr:
            print("warning: Node has no playwright, so cover.png and pitch.pdf were not made (CI makes them)",
                  file=sys.stderr)
            return False
        sys.exit(r.stderr)
    return True


def main():
    ident = yaml.safe_load((BOOK / "book.yml").read_text(encoding="utf-8"))
    OUT.mkdir(parents=True, exist_ok=True)
    DIST.mkdir(exist_ok=True)
    for name in ("cover.svg", "gila-monster.svg"):
        shutil.copy(BOOK / "images" / name, OUT / name)
    today = datetime.date.today().isoformat()
    pitch = ("A cookbook of network protocol recipes in which every example is a test that runs: a surl command that listens, "
             "a curl command that talks to it, and the result printed beside the claim. Every claim is checked against "
             "the current builds of the open-source programs it describes, and the book is built to be read by people "
             "and by machines.")
    index = json.loads((BOOK / "generated" / "index.json").read_text(encoding="utf-8"))["recipes"]
    titles = part_and_chapter_titles()
    status_html, parts_html = status_and_parts(index, titles)
    values = {**ident, "pitch": pitch, "pitch_one_line": pitch.split(":")[0] + ".", "date": today,
              "repo": REPO, "curl": CURL, "surl": SURL, "first_recipe": index[0]["key"],
              "copyright_cover": ident["copyright"].replace(" All rights reserved.", ""),
              "status_html": status_html, "parts_html": parts_html}
    shutil.copy(SITE / "surly.css", OUT / "surly.css")
    shutil.copy(SITE / "surly.js", OUT / "surly.js")
    cover = (SITE / "template.html").read_text(encoding="utf-8")
    for k, v in values.items():
        cover = cover.replace("{{" + k + "}}", str(v))
    (OUT / "index.html").write_text(cover, encoding="utf-8")
    (OUT / ".nojekyll").write_text("", encoding="utf-8")
    write_book_pages(values, index, titles)
    made_images = render_png_and_pdf()

    # Distribution copies of the pitch.
    shutil.copy(OUT / "index.html", DIST / "pitch.html")
    shutil.copy(OUT / "surly.css", DIST / "surly.css")
    if made_images:
        shutil.copy(OUT / "cover.png", DIST / "cover.png")
    shutil.copy(OUT / "cover.svg", DIST / "cover.svg")
    shutil.copy(OUT / "gila-monster.svg", DIST / "gila-monster.svg")
    md = f"""# {ident['title']}: {ident['subtitle']}

*Preview, {today}. {ident['author']}. {ident['publisher']}.*

{pitch}

## What it is

Every recipe is two commands facing each other: a `surl` that listens and a `curl` that talks to it. [Surl]({SURL}) is the server-side mate of curl, built to answer every scheme curl can request; [Curl]({CURL}) is a drop-in replacement for the curl command-line tool. Both are open source, both written in C# on .NET 10 as a matched pair, so each recipe shows a protocol conversation from both ends.

- **Recipes that run.** A recipe is an executable specification. Every claim the text makes is a test case run against the two programs, and the book prints the result beside the claim.
- **Simple to hard.** Ordered by difficulty, not by protocol. Open at any page; each recipe stands alone.
- **Readable by machines.** An index of every recipe by key, problem, protocol, option and exit code; the recipes' test specifications can also serve as a conformance suite for other implementations.

## How it is written

Written and checked with AI agents following a published method: every recipe passes three checks (shape, truth, review), and the author decides what is published. Every decision and its reasons are in the repository.

## The author

**{ident['author']}**, Principal Software Engineer, thirty years on the Microsoft stack. Author of Curl and Surl, both built with the AI-assisted process he designed. https://github.com/StewartScottRogers

Landing page: https://stewartscottrogers.github.io/Surly/ · Source: {REPO}

{ident['copyright']} Text CC BY-ND 4.0; example code Apache 2.0.
"""
    (DIST / "pitch.md").write_text(md, encoding="utf-8")
    txt = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"\1 (\2)", md)
    txt = re.sub(r"[*`#]+ ?", "", txt)
    (DIST / "pitch.txt").write_text(txt, encoding="utf-8")
    print(f"site at {OUT / 'index.html'}; distribution in {DIST}")


if __name__ == "__main__":
    main()
