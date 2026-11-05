# Surly - the cookbook

Copyright © 2026 Stewart Rogers. The prose is under Creative Commons Attribution-NoDerivatives 4.0 (read it, share it unchanged, learn from it, credit the author); the example code is under Apache 2.0 (use it, port it, keep the notice). See `LICENSE.txt`, `LICENSE-CODE.txt` and `RIGHTS.md`. The programs the recipes run are open source under their own licences in `../Curl/` and `../Surl/`.

Published under the **Oh Really Media** imprint. The title, subtitle, author and imprint are set in `book.yml` and nowhere else (ADR-0002).

A cookbook in the Problem / Solution / Discussion form. Every recipe pairs a `surl` command that listens with a `curl` command that talks to it, so each one demonstrates both programs at once.

| Where | What |
|---|---|
| `book.adoc` | The spine; includes the generated chapter order |
| `front-matter/` | Foreword (the author's own hand) and preface |
| `parts/<tier>/chapters/<chapter>/recipes/<key>/` | One folder per recipe: `recipe.adoc`, `run.sh`, `run.ps1`, `expected.txt`, its files, its images |
| `references/` | The bibliography by citation key, and local copies in `archive/` |
| `templates/` | Copy `recipe-template/` to start a recipe |
| `build/` | `build.py` and the setup steps |
| `editorial/log.md` | The author's decisions, the record of human authorship |
| `llms.txt` | Where a machine starts |
| `generated/`, `out/` | Build output, not committed |

Structure and rules: `../Documentation/Planning/Decisions/ADR-0004`.
