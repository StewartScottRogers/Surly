<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-measure-timings-under-a-delay
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.
## Findings
1. Untimed warm-up URL first in the same command: the first transfer's connect time carried about 1 s of set-up in probes.
2. Timings as ranges by regex (>= 1.5 s to first byte on /slow, < 1 s elsewhere).
3. Plain HTTP cannot tell an accept-stage hold from a server-side wait (both land in time_starttransfer); not claimed.
