<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-a-numbered-series-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. curl-mcp refuses -o with a #1 glob reference (PathRefused, before curl runs); the recipe uses -O --output-dir and shows the refusal as a case.
2. Cases share one root folder, so the program empties reports/ first; under verify the curl tool's standardOutput carried CRLF line ends, so the program trims each line.
