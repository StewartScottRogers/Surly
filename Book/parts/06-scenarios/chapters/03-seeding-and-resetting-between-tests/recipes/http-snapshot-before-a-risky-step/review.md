<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-snapshot-before-a-risky-step

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.

## Findings

1. Another server's snapshot gets the same message as a dropped one ('only its newest 4 snapshots are kept'), which is misleading for that case; reported, not worked around.
