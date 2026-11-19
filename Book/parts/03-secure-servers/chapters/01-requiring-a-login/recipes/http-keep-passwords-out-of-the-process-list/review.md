<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-keep-passwords-out-of-the-process-list

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.

## Findings

1. Fixed: the `#` rule is proved directly. The comment line is `#carol:retired`, which would be an account if read; logging in as it gets 401.
2. Truth (4): the process-list exposure and the Linux/macOS readable-file warning are cited to the README, not cases. The warning cannot be shown on Windows, and verify.py cannot see surl's log; check it on Linux at release.
3. Fixed: dropped "reads the file once, at start" (no case) and a mention of curl's own `--netrc-file`/`-K` (unlisted, unproved).
4. Not covered: the byte-order mark and CR LF line ends the aihelp promises. Could be a later case.
