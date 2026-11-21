<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-in-parallel-segments-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. Four transfer calls issued together do run side by side in curl-mcp (with the 1 s hold all four answered in about 1.5 s); claimed from get_events (four connections open at once), not from time.
2. 400,000 bytes keeps each part well under transfer's 1 MiB body limit and the seed message small.
- CI (re-pin, macOS): get_exchanges trailed the client and listed two of the four ranges; the program now asks again until all four are there, for up to 10 s.
