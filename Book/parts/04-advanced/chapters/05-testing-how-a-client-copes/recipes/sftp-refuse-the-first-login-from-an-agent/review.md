<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-refuse-the-first-login-from-an-agent
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100: 4 of 4 cases pass on windows.

## Findings

1. Carries the console claims (times=2, `--retry` alone 0 retries, `--retry-all-errors` 1, times=1 spent by the password, wrong password retried) through the `curl` tool, plus `get_events` login_judged per exchange and `list_faults` remaining.
2. `list_faults` shows the item as `refuse-login` without its `times`; only `remaining` tells the rules apart.
