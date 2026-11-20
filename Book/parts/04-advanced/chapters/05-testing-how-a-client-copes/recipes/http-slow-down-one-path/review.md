<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-slow-down-one-path
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. Found while probing: curl -Z without --parallel-immediate holds the second transfer for the first connection, so the fast path times out too. That is libcurl's PIPEWAIT, as the manual describes --parallel-immediate; kept as a claim because it is the hang the reader's task is about.
2. Timing claims use margins: a 5 s hold against a 2 s limit, lower bound only on the held answer.
