<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-offer-a-share-to-ftp-only-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 8 cases pass on windows.
## Findings
1. `curl -f` turns the HTTP 404 into exit 22, the plain test of whether a scheme reaches the share.
2. `start_server` with an unknown scheme is a tool error whose first line starts `surl: option --share`.
