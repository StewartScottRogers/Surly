<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fail-a-script-on-a-missing-file-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. start_server accepts an empty file in files; transfer's body for it and for a 404 are both empty, which is the recipe's point.
2. Re-pinned to Surl a423807a5d / Curl 0680b97577: transfer's 404 body is now surl's 404 Not Found (BL-1444); the body no longer looks like the empty file's, and the agent still reads statusCode.
