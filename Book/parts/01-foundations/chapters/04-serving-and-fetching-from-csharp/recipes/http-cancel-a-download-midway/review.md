<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-cancel-a-download-midway
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Distinct from http-cancel-a-stuck-transfer (a request held before any reply): this cancels a body mid-stream once the program has what it needs.
2. Server-side bytes sent were 1.5-1.9 MB locally; claimed as under 16 MiB to hold on Linux and macOS socket buffers.
