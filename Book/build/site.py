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


def first_recipe_body():
    first = next(recipes())
    rendered = BOOK / "out" / "recipes" / f"{first[3]['key']}.html"
    if not rendered.exists():
        sys.exit("run build.py first")
    text = rendered.read_text(encoding="utf-8")
    m = re.search(r'<div id="content">(.*?)</div>\s*<div id="footer">', text, re.S)
    body = m.group(1) if m else text
    body = body.replace('class="sect1"', '').replace('class="sectionbody"', '')
    body = body[: body.find('<h2 id="_references">')] if '<h2 id="_references">' in body else body
    # Links: another recipe is its own page; a citation is the bibliography on this recipe's page.
    keys = {spec["key"] for _, _, _, spec in recipes()}
    own = f"recipes/{first[3]['key']}.html"
    body = re.sub(r'href="#([^"]+)"',
                  lambda m: f'href="recipes/{m.group(1)}.html"' if m.group(1) in keys else f'href="{own}#{m.group(1)}"', body)
    return unlink_examples(body)


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


def write_book_pages(values, index, titles):
    """contents.html, recipes/<key>.html and book.html, all in the site's look."""
    keys = {r["key"] for r in index}
    (OUT / "recipes").mkdir(parents=True, exist_ok=True)
    part_numbers = {p: i + 1 for i, p in enumerate(dict.fromkeys(r["part"] for r in index))}

    # Contents: parts, chapters and recipes in book order.
    body, seen_part, seen_chapter = ["<h1>Contents</h1>"], None, None
    for r in index:
        if r["part"] != seen_part:
            if seen_chapter:
                body.append("</ol>")
            n = part_numbers[r["part"]]
            title, about = titles.get(r["part"], (r["part"], ""))
            body.append(f'<h2 id="part-{n}">Part {n}. {html.escape(title)}</h2><p>{inline(about)}</p>')
            seen_part, seen_chapter = r["part"], None
        if r["chapter"] != seen_chapter:
            if seen_chapter:
                body.append("</ol>")
            body.append(f'<h3>{html.escape(titles.get(r["chapter"], (r["chapter"], ""))[0])}</h3><ol>')
            seen_chapter = r["chapter"]
        body.append(f'<li><a href="recipes/{r["key"]}.html">{html.escape(r["title"])}</a>{badges(r)}'
                    f'<p>{inline(r["problem"])}</p></li>')
    body.append("</ol>")
    (OUT / "contents.html").write_text(
        page(values, "Contents", "Every recipe in the book, by part and chapter.",
             '<div class="contents">' + "\n".join(body) + "</div>", current="contents"), encoding="utf-8")

    # One page per recipe.
    for i, r in enumerate(index):
        content = rendered_content(BOOK / r["rendered"])
        # A link to another recipe is #key in a one-recipe page; here it is that recipe's own page.
        content = cited_references_only(content)
        content = re.sub(r'href="#([a-z0-9-]+)"',
                         lambda m: f'href="{m.group(1)}.html"' if m.group(1) in keys else m.group(0), content)
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

    # The whole book on one page.
    (OUT / "book.html").write_text(
        page(values, "The whole book", values["pitch_one_line"], rendered_content(BOOK / "out" / "surly.html"),
             current="book"), encoding="utf-8")


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
             "a curl command that talks to it, and the proof printed beside the claim. Written to a published standard, "
             "verified against the open-source programs it describes, and built to be read by people and by machines.")
    index = json.loads((BOOK / "generated" / "index.json").read_text(encoding="utf-8"))["recipes"]
    titles = part_and_chapter_titles()
    status_html, parts_html = status_and_parts(index, titles)
    values = {**ident, "pitch": pitch, "pitch_one_line": pitch.split(":")[0] + ".", "date": today,
              "repo": REPO, "curl": CURL, "surl": SURL, "recipe_html": first_recipe_body(),
              "status_html": status_html, "parts_html": parts_html}
    shutil.copy(SITE / "surly.css", OUT / "surly.css")
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

Every recipe is two commands facing each other: a `surl` that listens and a `curl` that talks to it. [Surl]({SURL}) is the server-side mate of curl, answering every scheme curl can request; [Curl]({CURL}) is a drop-in replacement for the curl command-line tool. Both are open source, both written in C# on .NET 10 as a matched pair, so each recipe shows a protocol conversation from both ends.

- **Recipes that run.** A recipe is an executable specification. Every claim the text makes is a test case run against the two programs, and the book prints the result beside the claim.
- **Simple to hard.** Ordered by difficulty, not by protocol. Open at any page; each recipe stands alone.
- **Readable by machines.** An index of every recipe by key, problem, protocol, option and exit code; the recipes' test specifications double as a conformance suite for any port, in any language.

## How it is written

Written and maintained over a twenty-year horizon by an unattended production line of AI agents working from a published standard: a recipe schema, three gates (shape, truth, review), and a written default for every decision so work never waits. The author sets direction and accepts or sends back each recipe; nothing is published without that line.

## The author

**{ident['author']}**, Principal Software Engineer, thirty years on the Microsoft stack. Author of Curl and Surl, both built by the unattended dark factory he designed. https://github.com/StewartScottRogers

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
