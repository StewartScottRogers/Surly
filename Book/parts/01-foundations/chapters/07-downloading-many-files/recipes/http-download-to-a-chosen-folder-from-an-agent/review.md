<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-to-a-chosen-folder-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Program finding, not claimed: curl-mcp resolves -o against CURL_MCP_ROOT ignoring --output-dir, so '--output-dir downloads -o ../guide.txt' (which real curl writes inside the root) is refused PathRefused. Safe but stricter than curl.
2. Refusal texts hold absolute temp paths, so those cases use patterns.
