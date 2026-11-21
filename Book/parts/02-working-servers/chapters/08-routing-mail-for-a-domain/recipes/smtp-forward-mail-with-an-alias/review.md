<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-forward-mail-with-an-alias
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. Overlaps smtp-route-mail-through-aliases on purpose (one alias, the three start refusals); this recipe is the plain first look, that one is the scenario.
3. Delivery wait is a bounded retry of an IMAP fetch (seed), never a sleep; Windows never lagged, the guard is for macOS CI.
