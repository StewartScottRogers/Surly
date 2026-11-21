<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-copy-reports-to-a-windows-share
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100: 10 of 10 cases pass on windows.

## Findings

1. Scenario: copy (two uploads), read over HTTP, the server's log of the writes, a corrected re-run, and five failure paths (wrong password 67, missing share 78, missing folder 78, uploads off 9, no `--auth ntlmv1` 67).
2. The log case matches `reports\x5C...`: SMB paths are logged with the backslash escaped; assumed the same on Linux and macOS (not run locally).
3. Content-Type `text/csv` for `.csv` was not in http.md; added.
