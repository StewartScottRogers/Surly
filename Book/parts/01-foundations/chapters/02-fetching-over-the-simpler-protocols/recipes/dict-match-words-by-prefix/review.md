<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dict-match-words-by-prefix

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 8 of 8 cases pass on windows.

## Findings

1. Overlaps dict-look-up-a-word on m: with the default strategy; this recipe adds the explicit strategy, named database, show:strat, 551, case and the pick.
