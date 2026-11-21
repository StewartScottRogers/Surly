<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-refuse-the-first-login
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100: 6 of 6 cases pass on windows.

## Findings

1. The planned "first login refused" needs `times=2`, not `times=1`: curl makes two login attempts (password, then keyboard-interactive) on one connection. Recipe states it and has a case for `times=1` passing at once.
2. `curl --retry 1` alone does not retry exit 67; the Solution uses `--retry-all-errors`, whose cost (a wrong password is retried too) has its own case.
3. The `||` / `&&` case runs under cmd on Windows and sh elsewhere; both group `a || b && c` as `(a || b) && c`. Linux and macOS not run locally.
