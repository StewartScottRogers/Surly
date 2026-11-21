<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-split-options-across-config-files-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 13 of 13 cases pass on windows.
## Findings
1. Needs the author: surl-mcp refuses `-K`, so the agent twin cannot read the console recipe's files; it layers argument lists in the same order and shows the refusal (case `config-refused`). It is the nearest honest recipe.
2. Reader: the solution case is slow (two hosts, several curl-mcp calls) and timed out once under load at 60 s.
