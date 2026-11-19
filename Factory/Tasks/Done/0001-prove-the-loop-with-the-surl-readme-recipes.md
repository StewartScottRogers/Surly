# Draft the recipes the Surl README already contains
key: prove-the-loop-readme-recipes
kind: recipe
depends: []
status: done
## Why
The Surl README holds about a dozen worked surl/curl pairs. Drafting them as recipes proves the template, the gates and the factory loop end to end before the book is written in earnest (ADR-0006 item 6).
## Acceptance
- [ ] One recipe folder per README pair, each with recipe.yml (solution plus every claim as a case) and recipe.adoc
- [ ] Each placed by the tier default in Policy/Defaults.md, with the default recorded in its Log
- [ ] lint clean; build clean; verify run (binaries may be absent: record that)
- [ ] Every decision taken by default listed in Factory/Decisions-Inbox.md
## Log
- 2026-11-19: done by other means. The README's surl/curl pairs, and far more, are now the book's 100 recipes in five parts (Book/editorial/chapter-list.md), each with recipe.yml, recipe.adoc and review.md, lint clean, build clean, verified against local builds. The loop this task was to prove ran as the lead session plus parallel writer agents; Factory/ itself has not yet run a shift.
