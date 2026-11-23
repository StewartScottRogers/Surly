<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-check-a-device-fetched-its-config
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, no packages: 7 of 7 cases pass on windows.

## Findings

1. check.sh polls surl's -v log at most ten times a second apart; a read counts only with its 'All ... were acknowledged' line, tied by exchange number.
2. Needs a POSIX shell for check.sh; run lines use && and || only, which cmd.exe and sh share.
3. Linux and macOS not run here; the log format is surl's, not the platform's.
