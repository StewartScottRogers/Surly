<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-a-unix-socket-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, dotnet 11.0.100: 5 of 5 cases pass on windows.
## Findings
1. SurlServerOptions has no Unix socket property; the program passes `--unix-socket` in AdditionalArguments, which works. A first-class property would read better (suggestion for Surl).
2. StopAsync removes the socket file on Windows too (in-process stop is graceful).
3. The listen URL names {{port}}, a free port, so the no-tcp case proves nothing listens there.
4. CI run 44906624868: on macOS the fetch aborted the program (134) and left surl.sock, so the next cases got 45 Address already in use. Socket file per case, StopAsync in finally; solution and host-is-only-a-name are [windows, linux] until Surl fixes the macOS defect. 5 of 5 pass on windows.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl now words a taken socket path `Could not bind <path>: the path exists already`; file-exists restated. BL-1565 (the macOS accept crash) is not fixed in a423807a5d, so the fetch cases stay [windows, linux].
Re-pinned to Surl 5da5b3eaf / Curl c84035f797: Surl fixes the macOS crash on the first Unix-socket accept (BL-1565, BL-1568), so the fetch cases run on all three platforms and the macOS-only failure text is gone; macOS is confirmed only by CI.
