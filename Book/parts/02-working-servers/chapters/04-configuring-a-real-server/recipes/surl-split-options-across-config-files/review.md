<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-split-options-across-config-files
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 12 of 12 cases pass on windows.
## Findings
1. Truth: every claim is about the last-read option winning, tried with limits and uploads over HTTP (413, 201, 405) and FTP (70) so that the server's behaviour shows which setting was in force.
2. Fixed: each server has its own directory (two surls cannot share one), the command-line `--directory` giving that is itself the claim `command-line-directory`; upload names are distinct per case, since an existing name answers 204, not 201.
