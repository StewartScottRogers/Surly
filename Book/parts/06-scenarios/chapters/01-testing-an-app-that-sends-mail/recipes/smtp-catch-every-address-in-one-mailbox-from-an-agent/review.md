<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-every-address-in-one-mailbox-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. The start_server refusal (exit_code 2, FailedInit, the message in lines) is cased; it agrees with the console twin's needs-mail-domain case.
