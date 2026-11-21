<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-keep-a-retained-message-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl-mcp and curl-mcp from surl 1.0.0 (Surl commit 9ad60d80c) and the Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.

## Findings

1. Carries every console claim (kept under the filter, later subscriber, replaced, per topic, outside the filter, no filter, cleared, refused without anonymous) and adds the seed.
2. Client publishes are followed by `wait_for_event` with `exchange_ended` and `after_sequence`, bounded at 10 s, not a sleep. A seed needs no wait.
3. The macOS stale read is not shown locally, so the prose says it can happen and no case claims it.
