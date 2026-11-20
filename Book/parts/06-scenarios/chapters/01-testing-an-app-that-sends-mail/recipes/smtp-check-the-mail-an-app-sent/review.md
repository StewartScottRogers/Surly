<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-the-mail-an-app-sent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited (<<surl-readme>> for discard-without-telling, <<rfc5321>> for Return-Path).
2. The 'fail' is a comparison the reader's test makes: curl exits 0 for a wrong subject or sender (SEARCH answers `* SEARCH`); only a wrong recipient gives a non-zero exit (78). Said plainly in the prose.
3. Cases are order-dependent (ann's UID 1 from the solution; bob's UIDs from the failure cases), as in Part 2's mail recipes.
