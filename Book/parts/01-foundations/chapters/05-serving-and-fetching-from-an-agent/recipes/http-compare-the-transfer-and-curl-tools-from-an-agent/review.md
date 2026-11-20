<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-compare-the-transfer-and-curl-tools-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Program note: JsonSerializer.Serialize throws in a file-based app (reflection serialization off); the program escapes by hand.
2. The claim that only curl writes to disk rests on transfer having no output field and on http-run-curl-from-an-agent for the root confinement.
