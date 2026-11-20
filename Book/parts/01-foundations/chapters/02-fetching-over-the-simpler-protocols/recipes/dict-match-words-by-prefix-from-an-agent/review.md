<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dict-match-words-by-prefix-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. Twin of the console recipe; a 552 or 551 is exitCode 0 and not a tool error, so the program reads the status line.
