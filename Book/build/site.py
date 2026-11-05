#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Build the landing page and the distribution formats of its pitch.

Reads book.yml (identity), site/template.html, images/cover.svg and the first
recipe's rendered body, and writes:
  out/site/            the landing page and everything it needs (what GitHub Pages serves)
  dist/                the pitch for handing out: pitch.html, pitch.pdf, pitch.md, pitch.txt, cover.png, cover.svg
Run build.py first. Needs Node with playwright (for the cover PNG and the PDF).
"""
import datetime, html, re, shutil, subprocess, sys
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
    return body[: body.find('<h2 id="_references">')] if '<h2 id="_references">' in body else body


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
        sys.exit(r.stderr)


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
    values = {**ident, "pitch": pitch, "pitch_one_line": pitch.split(":")[0] + ".", "date": today,
              "repo": REPO, "curl": CURL, "surl": SURL, "recipe_html": first_recipe_body()}
    page = (SITE / "template.html").read_text(encoding="utf-8")
    for k, v in values.items():
        page = page.replace("{{" + k + "}}", str(v))
    (OUT / "index.html").write_text(page, encoding="utf-8")
    (OUT / ".nojekyll").write_text("", encoding="utf-8")
    render_png_and_pdf()

    # Distribution copies of the pitch.
    shutil.copy(OUT / "index.html", DIST / "pitch.html")
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
