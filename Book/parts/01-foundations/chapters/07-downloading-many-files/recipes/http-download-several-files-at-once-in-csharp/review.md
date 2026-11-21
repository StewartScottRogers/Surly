<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-several-files-at-once-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.
## Findings
1. MaxConnectionsPerHost is the --parallel-max analogue; peak connections come from ExchangeStarted/ExchangeEnded events (query by kind, merge on Sequence). Capped: 4, uncapped: 12, serial and cap 1: 1.
2. Timings claimed as ranges (3-6 s capped, 1-2 s uncapped, 3 s or more serial).
