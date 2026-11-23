<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-see-exactly-what-an-app-sent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. dialogue.sh reads surl's --trace-ascii file: it waits (at most 10 s) for the last SMTP exchange's `ended` line, then prints Recv data as C: and Send data as S:. The 334 challenge is random, so the solution prints from MAIL FROM on.
3. The CRAM-MD5 answer is withheld as **** in the trace; the C# twin shows the raw record keeps it.
