<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-peek-at-headers-with-top-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. The after-delete case shows why an agent should key on UIDL identifiers, not numbers.
