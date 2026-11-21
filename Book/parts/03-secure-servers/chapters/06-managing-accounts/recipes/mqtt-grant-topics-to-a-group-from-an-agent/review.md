<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-grant-topics-to-a-group-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl-mcp and curl-mcp from surl 1.0.0 (Surl commit 9ad60d80c) and the Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.

## Findings

1. Carries the console claims: the role works, a second member, both wrong-role refusals, an account in no group, membership as the change, wrong password, clear password, and the three start refusals (as one case).
2. A refused subscription is an exit code in the curl tool's answer, not a tool error; a bad grant at start is a tool error of `start_server` with `exit_code` 2 `FailedInit`.
3. Publishes are followed by `wait_for_event` with `exchange_ended` and `after_sequence`, bounded at 10 s, not a sleep.
