<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-explain-a-405-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Allow is GET, HEAD without --allow-uploads and GET, HEAD, PUT with it; the agent tells the server-side fix from the client-side one by that header.
