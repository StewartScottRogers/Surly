<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-list-a-container-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. Same claims as the console recipe except none are dropped; the program prints the first line of each exception, since the Azure message carries RequestId and Time lines.
