# Editorial log

One line per decision by the author, newest last. This is the record of human authorship (ADR-0003): what was proposed, what the author decided, and why. Only the author's own decisions go here.

| Date | Recipe or item | Decision | Author's notes |
|---|---|---|---|
| 2026-11-05 | Book structure, imprint, licence split, AsciiDoc source | Accepted | Approved in conversation; see ADR-0001 to ADR-0004 |
| 2026-11-18 | ADR-0008: C# and MCP recipes | Accepted | Own recipes, own chapters inside the tiers, `#:package`, `Libcurl` advanced only |
| 2026-11-18 | Chapter list (`chapter-list.md`) | Accepted | 100 recipes in 24 chapters; Part 1 first, reviewed before Part 2; Part 5 kept whole, written last |
| 2026-11-18 | Part 1, Foundations: 22 recipes (chapters 1-5) | Proposed | All 130 cases pass against local unreleased builds on Windows; each recipe stays draft; verdicts and findings in each `review.md` |
| 2026-11-18 | Part 2, Working Servers: 24 recipes (chapters 1-6) | Proposed | All 217 cases pass against local unreleased builds (Surl a0236e175, Curl f4b30d981f) on Windows; each recipe stays draft; verdicts in each `review.md`; `smtp-seed-and-read-mail-in-process` renamed `imap-seed-and-read-mail-in-process` before publication |
| 2026-11-18 | Part 3, Secure Servers: 20 recipes (chapters 1-5) | Proposed | All 177 cases pass against local unreleased builds (Surl a0236e175, Curl f4b30d981f) on Windows; LDAP and HTTP Negotiate claims are Windows-specific and need Linux and macOS runs; keys and certificates are made by setup:, never committed |
| 2026-11-19 | MCP recipes as C# programs (ADR-0008 item 7 amended) | Accepted | The mcp: call list is retired; all 13 MCP recipes rewritten on the official C# MCP SDK |
| 2026-11-19 | Part 4, Advanced: 14 recipes (chapters 1-4) | Proposed | All 112 cases pass against local unreleased builds (Surl 993aa7072, Curl 0afbd10e3b) on Windows; two solutions use OpenSSH sftp where the Curl port cannot act as the client; each recipe stays draft |
| 2026-11-19 | Part 5, New Protocols: 20 recipes (chapters 1-4) | Proposed | All 253 cases pass against local unreleased builds (Surl 71807f05f, Curl 82913be53d) on Windows; all 100 recipes pass together; webtransport-open-a-stream can fail under heavy parallel load (a Curl defect, reported); each recipe stays draft |
| 2026-11-18 | Part 1, Foundations (22 recipes) | Accepted | Merged and published (ee70b2b) |
| 2026-11-18 | Part 2, Working Servers (24 recipes) | Accepted | Merged and published (7dc0b52) |
| 2026-11-19 | Part 3, Secure Servers (20 recipes) | Accepted | Merged and published (f36ec6d) |
| 2026-11-19 | Part 4, Advanced (14 recipes) | Accepted | Merged and published (a157251) |
| 2026-11-19 | Part 5, New Protocols (20 recipes) | Accepted | Merged and published (dcf78c4) |
| 2026-11-19 | Website: one look, folded book, search; cover without the embedded recipe, plainer copy | Accepted | Merged and published (811f3c5, a6da41d, 544cf82) |
| 2026-11-19 | Licence wording | Accepted | "All rights reserved" dropped from book.yml: the text is CC BY-ND 4.0 and the code Apache 2.0 (ADR-0003); the cover and mark keep their own rights (Book/README.md) |
| 2026-11-19 | The 1,000-recipe plan (plan-1000.md, ADR-0009) | Accepted | 990 recipes, 330 a surface; 890 planned in 15 batches; Part 6 Scenarios; triplets kept together in their task's chapter (ADR-0008 item 2 amended) |
| 2026-11-27 | http-accept-a-bearer-token, http-log-in-with-digest | Accepted | The author's review of the first five drafted recipes; verified on Surl v0.1.11 and Curl v0.4.0-preview.5 on all three platforms |
| 2026-11-27 | http-log-in-with-ntlm | Accepted | Kept the Negotiate sentence, which now cites curl-changes |
