<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-test-a-password-reset-mail-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The program writes reset.eml (the link names the bound HTTP port) into the folder curl-mcp is confined to, then sends it with -T.
2. The add_account counter-claim is cited to http-require-a-login, not cased here.
