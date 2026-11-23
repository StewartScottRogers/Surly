<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-upload-with-a-read-callback
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, Libcurl 0.2.0: 5 of 5 cases pass on windows.

## Findings

1. Pin from setup (ssh-keygen, pin.sh); wrong key is a second server with other_key (60); uploads off is a third server (9).
2. Upload checked on disk with cmp after the program exits; the SFTP close is acknowledged before the program returns.
3. The C listing is not compiled by verify.
