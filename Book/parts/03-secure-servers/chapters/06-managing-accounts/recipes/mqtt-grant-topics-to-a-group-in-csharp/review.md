<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-grant-topics-to-a-group-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): Surl.Api 1.0.0 (Surl commit 9ad60d80c), Curl.Api 0.2.0: 8 of 8 cases pass on windows.

## Findings

1. Carries the console claims: the role works, a second member, both wrong-role refusals, an account in no group, membership as the change, wrong password, clear password, and the three start refusals (here as one case).
2. A dropped publish is shown by `ReadRetainedAsync`, which bypasses the grants, so it holds the value; the console recipe shows it with a second subscription.
3. Publishes wait for `SurlEventKind.ExchangeEnded` after the last sequence, bounded at 10 s, not a sleep.
