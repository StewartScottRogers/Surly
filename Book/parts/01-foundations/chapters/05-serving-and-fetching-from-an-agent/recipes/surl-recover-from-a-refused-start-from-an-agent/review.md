<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-recover-from-a-refused-start-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Distinct from surl-recover-when-a-port-is-taken-from-an-agent (45): covers 58, 2, 37, 3 and an unknown code passed on (1).
2. surl-mcp's `lines` omit the `surl: ` prefix the command line prints; stated in the Discussion.
