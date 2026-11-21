<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-skip-unchanged-files-with-etags-in-csharp

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit a423807a5d), Curl port (Curl commit 0680b97577), local builds, 2026-11-21, windows: 6 of 6 cases pass.

## Findings

1. Re-pinned to Surl a423807a5d / Curl 0680b97577: the job is now GET with If-None-Match and surl's own 304 (BL-1449), replacing HEAD-then-GET on Last-Modified; title restored to 'Skipping Unchanged Files with ETags in C#' (and the plan row).
2. The ETag is write-time ticks plus length; with the TestClock frozen, the same-second rewrite is caught by its new length (claimed so). A same-length rewrite at a frozen clock would keep its tag; not shown.
3. Moving the clock back moves b.txt's Last-Modified to 15:04:00 (none later than Date) while its ETag stays; claimed in the older case.
