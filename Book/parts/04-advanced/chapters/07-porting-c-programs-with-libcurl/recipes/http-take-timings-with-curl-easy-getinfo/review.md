<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-take-timings-with-curl-easy-getinfo
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Libcurl 0.2.0: 4 of 4 cases pass on windows.

## Findings

1. Times are claimed as ranges with wide margins (first transfer under 2 s, delayed starttransfer at least 1.5 s, pretransfer under 1 s); a heavily loaded runner could still exceed 1 s for a cold pretransfer.
2. The C listing is not compiled; the port mirrors it line for line (prints the path, not the whole URL, so output carries no port).
3. No 4 s first-request cost showed in Libcurl's timers, so no warm-up transfer was needed.
