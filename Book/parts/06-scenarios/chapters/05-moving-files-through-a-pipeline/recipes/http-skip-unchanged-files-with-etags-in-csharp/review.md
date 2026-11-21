<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-skip-unchanged-files-with-etags-in-csharp

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 6 of 6 cases pass.

## Findings

1. Same retitling as the console twin. Sync is HEAD-then-GET on Last-Modified equality, so it does not depend on surl's conditional support.
2. SurlServerOptions.TimeProvider drives Last-Modified for in-memory files, which makes the same-second miss deterministic.
4. Moving the TestClock back five seconds also changed b.txt's Last-Modified (to 15:04:00): surl sends no Last-Modified later than its Date; claimed in the older-date case.
