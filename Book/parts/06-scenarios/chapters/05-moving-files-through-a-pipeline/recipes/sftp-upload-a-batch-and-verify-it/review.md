<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-upload-a-batch-and-verify-it
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Truth: the check is names and sizes, as the task asks, and the prose says that this does not prove the bytes equal.
2. Fixed: one `curl -T` with a glob uploads the batch, since each curl process costs seconds on a loaded machine; a server without `--list-directories` stops `compare.sh` with curl's 78 instead of a false mismatch.
3. Reader: the scripts need a POSIX shell (Git Bash on Windows); the host key is pinned with `--hostpubsha256` taken from `ssh-keygen -l`.
