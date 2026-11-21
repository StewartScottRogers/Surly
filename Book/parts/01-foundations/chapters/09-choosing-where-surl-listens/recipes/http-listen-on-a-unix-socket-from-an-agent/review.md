<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-a-unix-socket-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0, dotnet 11.0.100: 5 of 5 cases pass on windows.
## Findings
1. transfer has no socket field; the curl tool is the only way to a socket from curl-mcp. Both hosts confine the socket path to their root (surl-mcp: `refused: ... not inside the root`; curl-mcp: PathRefused).
2. Each scenario has its own socket file: a scenario that ends without stop_server left its file, and the next start on it got 45 (seen while writing).
3. CI run 44906624868: on macOS the curl tool's fetch through the socket failed CurlExit (the surl macOS Unix-socket defect, reported); solution is [windows, linux]. 5 of 5 pass on windows.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl now words a taken socket path `Could not bind <path>: the path exists already`; file-exists restated. BL-1565 (the macOS accept crash) is not fixed in a423807a5d, so the solution stays [windows, linux].
Re-pinned to Surl 5da5b3eaf / Curl c84035f797: Surl fixes the macOS crash on the first Unix-socket accept (BL-1565, BL-1568), so the fetch cases run on all three platforms and the macOS-only failure text is gone; macOS is confirmed only by CI.
