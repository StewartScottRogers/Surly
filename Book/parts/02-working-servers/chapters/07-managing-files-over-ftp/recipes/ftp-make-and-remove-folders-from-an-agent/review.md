<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-make-and-remove-folders-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 cases pass on windows.
## Findings
1. `clear-a-folder` is the planned story: refused RMD, list_files, DELE, RMD again.
2. Replies come from `get_exchanges` `sent` chunks of the exchange after the count seen before the call.
