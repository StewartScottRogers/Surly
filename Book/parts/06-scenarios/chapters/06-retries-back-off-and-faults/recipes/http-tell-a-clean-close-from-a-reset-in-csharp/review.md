<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-clean-close-from-a-reset-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): Surl master 1e5204d, Curl master f2331ebe (Surl.Api, Curl.Api): 8 of 8 cases pass on windows, three runs.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case; the macOS note is platforms.md (Curl BL-5799).
2. The bytes-kept check compares with the recorded exchange's Sent bytes less the head, so it does not depend on the head's size; only the solution pins 999,787 and 1000213.
3. FaultAppliedEvent does not carry the close mode, so the two endings are told apart by the verbose log line (or the Close the test set). Raised as a suggestion, not a bug.
