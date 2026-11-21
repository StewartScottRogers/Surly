<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-move-a-client-to-sftp-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. Carries the console claims: both fetches compared, both logins in the server log, listing, exit codes, pin and wrong pin, the cutover. Uses HostKeyFiles from setup (ssh-keygen).
