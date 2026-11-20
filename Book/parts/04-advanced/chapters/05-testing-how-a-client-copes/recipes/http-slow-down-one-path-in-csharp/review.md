<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-slow-down-one-path-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows (three runs).
## Findings
1. Fixed: the first draft printed under 1 s / 1 to 4 s from one shared clock and failed once on a cold first run (the engine start added about 2 s); each fetch now has its own stopwatch and reports only within or after the 5 s hold.
2. Two SendAsync calls on one CurlClient ran side by side; no PIPEWAIT hold-up as with curl -Z.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: under load the timed lines read "after 5 s" because the stopwatch held the first Curl.Api request's cold start (4 s even on an idle machine), not a Curl regression; the program now makes one untimed fetch first and the hold is ten seconds, five times the two-second limit.
