<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-keep-a-retained-message-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): Surl.Api 1.0.0 (Surl commit 9ad60d80c), Curl.Api 0.2.0: 8 of 8 cases pass on windows.

## Findings

1. Carries all seven console claims (kept under the filter, later subscriber, replaced, per topic, outside the filter, no filter, cleared, refused without anonymous) and adds the seed.
2. Client publishes wait for `SurlEventKind.ExchangeEnded` after the last sequence seen, bounded at 10 s, not a sleep; `RecordExchanges` has to be on. A seed needs no wait.
3. The macOS stale read is not shown locally, so the prose states it as the reason for the wait and no case claims it.
