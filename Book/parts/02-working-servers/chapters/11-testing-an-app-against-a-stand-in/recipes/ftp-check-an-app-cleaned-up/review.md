<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-check-an-app-cleaned-up
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 8 cases pass on windows.
## Findings
1. The check script lists into a variable before searching it: piping `curl -l` into grep would print clean when the listing failed (exit 19 case). A clean listing also passes for an app that never wrote (uploads-off cases), which the text says.
2. A dot-named temporary file cannot be uploaded (exit 25) and is not listed, so it is not claimed.
