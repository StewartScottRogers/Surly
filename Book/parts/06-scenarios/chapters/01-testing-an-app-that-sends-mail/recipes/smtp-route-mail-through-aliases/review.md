<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-route-mail-through-aliases
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited to <<surl-readme>>.
2. The app's two sends are `seed:` (shown in the Solution, not a claim); the solution checks where they landed. Cases are order-dependent (ann's UIDs 1 to 3).
3. Alias loops (`includes itself`) were probed but are not claimed; the fact sheet has the message.
