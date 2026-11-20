<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: curl-build-test-urls-safely
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Finding worth a note (libcurl behaviour, not a bug): build_url without UrlEncode answers a malformed URL with no failure; parse_url refuses it. That is the recipe's failure path.
2. --allow-plaintext-auth has its refusal case (in-clear: 403 and 530/67, no login event).
