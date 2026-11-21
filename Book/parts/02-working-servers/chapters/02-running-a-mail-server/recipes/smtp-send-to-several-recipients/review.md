<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-to-several-recipients
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to the bibliography.
2. Each case has mailboxes of its own so counts do not depend on case order.
3. The stored-once claim reads surl's verbose log (Message stored: N bytes for 3 recipients); the twins use the log (C#) and the exchange (agent).
