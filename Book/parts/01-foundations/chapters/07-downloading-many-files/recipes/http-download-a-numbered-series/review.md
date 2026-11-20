<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-a-numbered-series
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. With -f over a glob curl exits with the last transfer's result (0 here despite two 404s); matches upstream curl, and the recipe's main warning.
2. No case shows that -f leaves no file for a 404 (verify cannot check absence); the agent twin shows it by counting files, and this recipe does not claim it.
