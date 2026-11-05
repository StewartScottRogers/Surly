# Instructions for the AI that writes this book

Read this before touching anything under `Book/`. It is the whole method; the decision records in `../Documentation/Planning/Decisions/` say why.

## What you are doing

You are writing and maintaining *Surly*, a cookbook in which every recipe pairs a `surl` command that listens with a `curl` command that talks to it. The author, Stewart Rogers, writes no line himself except the foreword; he directs and decides. You propose, run, judge and revise. Every recipe is three things at once: an explanation a person can follow, an integration test of Surl and Curl, and a machine-checkable claim.

## Where things are

- `llms.txt` maps the book. `generated/index.json` (after a build) lists every recipe with its metadata; read it before reading any recipe.
- A recipe is a folder under `parts/<tier>/chapters/<chapter>/recipes/<key>/` holding `recipe.yml` (the executable specification: the recipe), `recipe.adoc` (the prose, which includes generated blocks), `verified.yml` (written by verify, never by hand), and fixtures.
- `templates/recipe-template/` is the starting point for a recipe. `REVIEW.md` is the rubric. `references/bibliography.adoc` holds every off-site source by citation key. `editorial/log.md` is the author's decisions.

## To add a recipe

1. Pick the key: lower-case words joined by hyphens, starting with the protocol, naming the task (`ftp-accept-uploads`). It never changes once published.
2. Pick the tier by difficulty, not protocol: `foundations` (one listener, one request, no login, no encryption), `working-servers` (uploads, listings, several listeners, state), `secure-servers` (accounts, Transport Layer Security, host keys, certificates), `advanced` (edge cases, interoperability, programs using `Curl.Api` or `Libcurl`), `new-protocols` (anything either program learned recently; see below). Put it in the chapter whose task it belongs to; create the chapter with a `chapter.adoc` if none fits.
3. Copy the template. Write `recipe.yml` first: the solution case, then one case for every claim the Discussion will make, including the refusals and the failures. A claim you cannot write a case for does not go in the Discussion.
4. Write `recipe.adoc`: Problem (one or two sentences, the reader's words), Solution (one sentence, then the generated block), Discussion (what each option does here, why the server refuses what it refuses, what the exit codes mean; cite the bibliography by key), See Also.
5. Run, in this order, and fix until all pass: `python3 build/lint.py`, `python3 build/verify.py <key>`, `python3 build/build.py`. Look at `out/recipes/<key>.html`.
6. Review the recipe against `REVIEW.md` as if you had not written it, and write `review.md` beside it with the verdict and findings. Fix what you can; leave what needs the author.
7. Add a line to `editorial/log.md` proposing it, with the verdict, for the author's decision. Commit with a message that names the key.

## To add a protocol

A protocol that is new to either program gets its first recipes in `parts/05-new-protocols/chapters/<protocol>/`. When the protocol is released in both programs and its recipes have been verified against a release on all three platforms, move each recipe forward into the tier its difficulty puts it in, in one commit per recipe, and record the move in the editorial log. The key does not change when the folder moves.

## When a program releases

Run `python3 build/verify.py` with the new binaries on each platform. Every `broken` recipe is a finding: decide whether the program regressed or the book was wrong, write it in the editorial log, and either file it against the program or fix the recipe. Rebuild so the book shows the new status. Never edit `verified.yml`.

## Rules that do not bend

- `recipe.yml` is the recipe. Never edit anything under `.generated/`, `run.sh`, `run.ps1` or `verified.yml`; regenerate them.
- The book is public and under Creative Commons Attribution-NoDerivatives 4.0 for prose and Apache 2.0 for code; every file carries the copyright line `Copyright (c) 2026 Stewart Rogers`. Nothing from O'Reilly Media or any publisher: no names, series names, trade dress.
- Nothing off-site is pasted in. A source goes in the bibliography under a key, with a copy in `references/archive/` where its licence allows, and the recipe cites the key.
- Spell out every acronym on first use in each recipe. Fahrenheit for any temperature. No exclamation marks; no "simply" or "just".
- The voice: plain, direct, dry. The server is surly; the author is not.
- Upstream curl is a yardstick during the programs' bring-up, not a subject. Recipes name `surl` and `curl`, meaning this project's two programs.
- Ask the author only when a decision cannot be undone and could reasonably go either way. Otherwise take the reasonable reading, say which you took in the editorial log, and carry on.
- Check `RIGHTS.md` when an edition is near and raise any unticked box the author owns.

## The schema

`recipe.yml` is `schema: 1`, defined by `build/recipes.py` and ADR-0005. If a recipe needs something the schema cannot say (a compiled program as the client, an interactive session, a server-log assertion), do not improvise in the prose: add it to the schema with a decision record and a schema version, in `build/recipes.py` first so lint, verify and build stay in step.
