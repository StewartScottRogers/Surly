<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-offer-a-single-cipher
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Truth: the agreed algorithms are read from `sftp -v` (OpenSSH), not from curl, which has no option to narrow its own offer and prints nothing about the cipher; the claims use `sftp` for that and `curl` only to show it still connects.
2. Reader: `2>&1` is needed because `sftp -v` writes its debug lines to standard error; the host-key check is skipped, as in the weak-algorithms recipe.
3. Fixed: a cipher/MAC outside the allowed pair is a case each (`other-cipher-refused`, `other-mac-refused`), plus the unknown-name and weak-name refusals.
4. Reader: the first run of the recipe under a heavily loaded machine timed out once at 30 s (an `sftp` with a 3072-bit key); it passed on rerun.
