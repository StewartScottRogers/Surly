# Editorial log

One line per decision by the author, newest last. This is the record of human authorship (ADR-0003): what was proposed, what the author decided, and why. Only the author's own decisions go here.

| Date | Recipe or item | Decision | Author's notes |
|---|---|---|---|
| 2026-11-05 | Book structure, imprint, licence split, AsciiDoc source | Accepted | Approved in conversation; see ADR-0001 to ADR-0004 |
| 2026-11-18 | ADR-0008: C# and MCP recipes | Accepted | Own recipes, own chapters inside the tiers, `#:package`, `Libcurl` advanced only |
| 2026-11-18 | Chapter list (`chapter-list.md`) | Accepted | 100 recipes in 24 chapters; Part 1 first, reviewed before Part 2; Part 5 kept whole, written last |
| 2026-11-18 | Part 1, Foundations: 22 recipes (chapters 1-5) | Proposed | All 130 cases pass against local unreleased builds on Windows; each recipe stays draft; verdicts and findings in each `review.md` |
| 2026-11-18 | Part 2, Working Servers: 24 recipes (chapters 1-6) | Proposed | All 217 cases pass against local unreleased builds (Surl a0236e175, Curl f4b30d981f) on Windows; each recipe stays draft; verdicts in each `review.md`; `smtp-seed-and-read-mail-in-process` renamed `imap-seed-and-read-mail-in-process` before publication |
