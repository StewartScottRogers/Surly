<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-sign-a-run-of-s3-requests-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. login_judged events are asserted in full for the run (8 events); for the read-only server they are not asserted (3 events for 4 requests: not every refusal is judged).
