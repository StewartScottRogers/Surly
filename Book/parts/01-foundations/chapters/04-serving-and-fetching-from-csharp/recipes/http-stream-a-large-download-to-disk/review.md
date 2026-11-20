<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-stream-a-large-download-to-disk
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 2 of 2 cases pass on windows.
## Findings
1. Memory is measured as the live managed heap after full collections; observed 0 MiB streaming vs ~100 MiB buffered, against a 16 MiB threshold, so wide margins.
2. The server serves from DataDirectory so the file is not in the in-process store; the program writes the file itself (seeded Random), no fixture. Runtime about 10-20 s per case on Windows.
