<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-subscribe-over-websocket-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl.Api 1.0.0 (Surl commit 5da5b3eaf), Curl.Api (Curl commit c84035f797): 11 of 11 cases pass on windows.

## Findings

1. Carries 11 of the console's 12 claims. Not carried: publishing over mqtt5+ws:// (Curl.Api POST over mqtt5 sends a SUBSCRIBE and keeps nothing); publishes go through mqtt:// on the same bus. Said in the Discussion.
2. Publishes wait for ExchangeEnded after the last sequence, bounded at 10 s, not a sleep.
