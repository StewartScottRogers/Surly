<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-tell-the-refusals-apart-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 12 of 12 cases pass on windows.

## Findings

1. All console claims carried, plus `get_events` `login_judged`: the unsigned request is never judged, the unknown key and wrong secret are refused, the right key accepted. Only the first three events are asserted; a later request (a missing bucket) did not appear in the list, which is not claimed.
