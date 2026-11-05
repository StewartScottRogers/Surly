# Draft the recipes the Surl README already contains
key: prove-the-loop-readme-recipes
kind: recipe
depends: []
status: ready
## Why
The Surl README holds about a dozen worked surl/curl pairs. Drafting them as recipes proves the template, the gates and the factory loop end to end before the book is written in earnest (ADR-0006 item 6).
## Acceptance
- [ ] One recipe folder per README pair, each with recipe.yml (solution plus every claim as a case) and recipe.adoc
- [ ] Each placed by the tier default in Policy/Defaults.md, with the default recorded in its Log
- [ ] lint clean; build clean; verify run (binaries may be absent: record that)
- [ ] Every decision taken by default listed in Factory/Decisions-Inbox.md
## Log
