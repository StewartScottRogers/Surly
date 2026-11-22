<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-repeat-a-failure-with-a-fixed-seed-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
6 of 6 cases pass on Windows.
1. Same S3-for-HTTP/FTP substitution as the console twin (finding 1 there).
2. No-seed case left out, as in the C# twin. Each curl-mcp `curl` call is its own connection, so the probe shifts the exchange as on the console.
