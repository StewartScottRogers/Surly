<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-fail-one-command
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. One rule per account keeps the cases independent on one server (a rule spent by one case cannot affect another).
3. Readiness connection does not spend the rules (they act on the command); the seed waits on Fay, who has no rule, because a FETCH wait on a ruled account would spend it.
