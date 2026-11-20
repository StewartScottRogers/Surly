<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-list-the-schemes-it-serves
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.
## Findings
1. Protocol and feature lists differ by platform and build; the cases check named members, not the whole line. The platform in surl's first line is matched as (.+).
