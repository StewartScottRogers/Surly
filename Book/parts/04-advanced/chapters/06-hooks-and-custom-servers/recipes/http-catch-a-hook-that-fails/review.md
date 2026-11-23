<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-catch-a-hook-that-fails
Verdict: accept
Checked against local builds on 2026-11-22: Surl v0.1.5 (d64faa4bb) and Curl 2145a49eae (the feed's Surl.Api 1.0.0 and Curl.Api 0.2.0 reuse those version numbers), dotnet 11.0.100: all cases pass on windows.
## Findings
1. A throwing or refused decision is served as normal and published as HookFailed (Threw); refused answers cannot act at the Command stage.
