<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-serve-a-plain-http-endpoint-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.

## Findings

1. `start_server` answers `log_lines` with the start-up warning, so the agent needs no log file; a start refusal is a tool error with `exit_code` 58.
2. `get_events` `login_judged` on the main server gave six events (the put refused for uploads off included); the claim reads the first four only.
