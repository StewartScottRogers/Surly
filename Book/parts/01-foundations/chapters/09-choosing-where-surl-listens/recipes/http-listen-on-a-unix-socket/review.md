<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-a-unix-socket
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. No servers: block: verify's readiness check waits on a TCP port, and a surl on a Unix socket opens none, so on-a-socket.sh starts surl and waits for the Listening on line (surl took 2-8 s to start on this loaded machine; cases have timeout 60). Needs a POSIX shell.
2. removed-on-stop is [linux, macos] (surl's help: the file is removed when surl stops); not seen locally. left-on-windows is [windows]: Git Bash's kill ends surl outright (platforms.md).
3. Each case uses its own socket file, since a killed surl leaves its file and the next start exits 45.
4. Observation for Surl: an absolute socket path over the platform limit fails `(45) Could not bind ...: Bind failed`, without saying the path is too long.
