<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-refuse-a-publish-by-reason-code-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl.Api 1.0.0 (Surl commit 5da5b3eaf), Curl.Api (Curl commit c84035f797): 9 of 9 cases pass on windows.

## Findings

1. Carries all nine console claims. Curl.Api POST over mqtt5:// sends a SUBSCRIBE and keeps nothing, so publishes go through a second mqtt:// listener on the same bus, accounts and grants.
2. Publishes wait for ExchangeEnded after the last sequence, bounded at 10 s, not a sleep.
