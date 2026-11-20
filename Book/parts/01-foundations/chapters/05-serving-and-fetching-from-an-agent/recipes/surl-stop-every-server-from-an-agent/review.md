<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-stop-every-server-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 3 of 3 cases pass on windows.
## Findings
1. Differs from surl-clean-up-leftover-servers by confirming free ports by rebinding each freed URL, including both listeners of a two-listener server; the busy case shows 45 BindFailed before the stop.
2. Small risk: another process could take a just-freed port between stop and rebind; not seen.
