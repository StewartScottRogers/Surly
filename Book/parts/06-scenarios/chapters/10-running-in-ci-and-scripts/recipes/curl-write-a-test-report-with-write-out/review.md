<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: curl-write-a-test-report-with-write-out
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. Uses %{json} rather than a hand-made format; the 000 pitfall of %{http_code} is a case.
2. run: lines avoid '|' and backslash-quote in grep patterns (cmd.exe would read the '|' as a pipe); fields are matched with several -e patterns.
