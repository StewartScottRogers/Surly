<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-a-unix-socket
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. No servers: block: verify's readiness check waits on a TCP port, and a surl on a Unix socket opens none, so on-a-socket.sh starts surl and waits for the Listening on line (surl took 2-8 s to start on this loaded machine; cases have timeout 60). Needs a POSIX shell.
2. removed-on-stop is [linux, macos] (surl's help: the file is removed when surl stops); not seen locally. left-on-windows is [windows]: Git Bash's kill ends surl outright (platforms.md).
3. Each case uses its own socket file, since a killed surl leaves its file and the next start exits 45.
4. Observation for Surl: an absolute socket path over the platform limit fails `(45) Could not bind ...: Bind failed`, without saying the path is too long.
5. CI run 44906624868: on macOS curl exits 55 through the socket (surl 1.0.0 binds it and prints Listening on); not the path length (relative, 9 bytes; a connect failure would be 7, a long path 6). Reported to Surl; solution and host-is-only-a-name are [windows, linux], new [macos] case fails-on-macos claims the 55. 9 of 9 pass on windows (10 cases, one macOS-only).
- CI (batch 9, macOS): diagnosed as a surl crash on the first Unix-socket accept (Surl BL-1565); the first client sees 52 or 55, so the macOS case accepts either.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl now words a taken socket path `Could not bind <path>: the path exists already`; file-exists and left-by-a-crash restated. BL-1565 is not fixed in a423807a5d (its last note: upstream curl does not trigger the crash, the Curl port does), so the macOS case still accepts 52 or 55.
Re-pinned to Surl 5da5b3eaf / Curl c84035f797: Surl fixes the macOS crash on the first Unix-socket accept (BL-1565, BL-1568), so the fetch cases run on all three platforms and the macOS-only failure text is gone; macOS is confirmed only by CI. The `fails-on-macos` case is removed.
- CI (Linux): in no-tcp-port the Listening line was once missing from the captured output; the case matches curl's 7 at the end whether or not a line precedes it (the claim is the exit code).
