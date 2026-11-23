<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-many-transfers-with-a-multi-handle
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, Libcurl 0.2.0: 4 of 4 cases pass on windows.

## Findings

1. Overlap shown by time (three 3 s holds done in under 6 s, ~3.3 s locally) and by order (the fast file, added last, reported first); the held files' order is not claimed.
2. The port drops CURLOPT_WRITEDATA/CURLOPT_PRIVATE (closures) and adds the elapsed time to the last line; both stated.
3. The C listing is not compiled by verify.
