<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-read-headers-with-curl-easy-header
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Libcurl 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. Codes checked against upstream headers.c: NOHEADERS 3 before a transfer, MISSING 2, BADINDEX 1, NOREQUEST 4; the port matches.
2. The walk prints only the names of Date and Last-Modified, whose values change per run; stated in the Discussion.
3. The C listing is not compiled by verify.
