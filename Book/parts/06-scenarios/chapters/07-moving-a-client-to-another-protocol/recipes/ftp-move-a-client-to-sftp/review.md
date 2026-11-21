<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-move-a-client-to-sftp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. One surl serves ftp and sftp from one directory on two ports (TCP both). The old client needs --allow-plaintext-auth; the cutover server omits it and refuses the old client (67) while serving SFTP. Needs ssh-keygen and a POSIX shell for the pinned case.
2. Without -k and no pin, curl's SFTP failed here with exit 2 (no known_hosts file); not claimed since it may differ per platform.
