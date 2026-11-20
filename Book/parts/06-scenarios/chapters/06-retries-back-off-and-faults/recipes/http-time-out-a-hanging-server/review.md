<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-time-out-a-hanging-server
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. Hold is 5 s against -m 2: margins of 3 s.
2. The server log records the hold ('GET held for 5 s by fault rule f1') for the request whose client gave up.
3. Overlaps http-slow-down-one-path on delay mechanics; this one adds --connect-timeout, retry after a timeout and a server-side count.
