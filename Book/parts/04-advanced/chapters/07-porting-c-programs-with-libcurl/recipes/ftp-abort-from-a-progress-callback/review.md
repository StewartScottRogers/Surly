<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-abort-from-a-progress-callback
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, Libcurl 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. Program finding (Curl): the xferinfo callback is not called while the server holds the transfer (upstream: about once a second), so the abort lands when data first arrives, after the 3 s hold; stated in the Discussion and cased (first-call-with-data).
2. Program finding (Curl): CURLINFO_TOTAL_TIME_T read inside the callback is 0 (upstream: time so far); the port times itself with a Stopwatch where the C listing calls time().
3. Elapsed seconds claimed as 3 or more; the C listing is not compiled by verify.
