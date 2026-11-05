# ADR-0005: A recipe is an executable specification; the prose is a view of it

## Status

Accepted, 2026-11-05. Refines items 2, 4, 5 and 14 of ADR-0004.

## Context

The book is written, run, judged and maintained by AI for twenty years. A recipe must therefore be three things at once: an explanation a person can follow, an integration test of Surl and Curl talking to each other, and a machine-checkable claim a future AI or a port in another language can validate against. The first template kept a script beside the prose; nothing tied the Discussion's claims to anything that ran.

## Decision

1. **`recipe.yml` is the recipe.** It holds the metadata, the fixtures, the servers to start, and the cases. `recipe.adoc` holds only the prose and includes what the build generates from the specification. Edit the specification; never the generated files.
2. **Every claim is a case.** Each case is one sentence of claim, one command, and what must be true afterwards: exit code, exact stdout or a pattern, stderr content, files on disk. Exactly one case has `role: solution` and is printed under Solution; every other case is printed under Discussion as a checked claim with its command and result. A Discussion claim with no case is a lint failure.
3. **Placeholders keep the book stable and the test honest.** `{{port}}` is shown as 8080 in the book and allocated free at run time; `{{null}}` is `/dev/null` or `NUL` by platform. New placeholders are added to `build/recipes.py`, which the build, the verify and the lint all import, so the three cannot disagree.
4. **Verification moves the status; nobody else does.** `verify.py` runs every case against the `surl` and `curl` on the path (or named by `SURLY_SURL` and `SURLY_CURL`), in a temporary directory with a free port, and writes `verified.yml`: `verified` or `broken`, the exact versions, the date, the platforms that passed, and each case's result. The book prints that line under the Solution. A new release of either program re-runs every recipe; a recipe that breaks is a finding against the program or the book, and the editorial log says which. This is how the book becomes Surl's conformance suite for the Curl port.
5. **Three gates before `published`.** `lint.py` for shape (sections, anchors, copyright, every option explained, every reference cited and resolving, at least one checked claim beyond the solution). `verify.py` for truth. An AI review against `REVIEW.md` for fitness, truth and voice, filed as `review.md` beside the recipe. The author's decision on top, in `editorial/log.md`.
6. **The schema is versioned** (`schema: 1`) so the format can grow. Expected additions, each a new schema version and decision record: cases that compile and run a program rather than a shell line (for `Curl.Api`, `Libcurl` and later Rust); interactive and streaming sessions; several servers sharing state; assertions on server logs; platform-scoped cases; generated fixtures such as certificates and host keys.

## Consequences

- The printed Solution, `run.sh`, `run.ps1`, the expected output and the checked-claims table are all generated from one specification and cannot drift from each other or from what was tested.
- A recipe can be judged by a machine on all three qualities the author asked for: fitness (does the solution case satisfy the Problem), truth (do the claims pass), and explanation (is every claim a case, is every option explained).
- A future port can take `recipe.yml` alone as its acceptance test.
- `verify.py` needs real binaries; until Surl and Curl are released, every recipe stays `draft`, and the book says so on each one.
