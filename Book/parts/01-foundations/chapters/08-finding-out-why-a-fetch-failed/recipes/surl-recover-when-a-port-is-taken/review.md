<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-recover-when-a-port-is-taken
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. The holder is another surl with no --directory, so the script's surl can serve site.
2. 'Address already in use' was seen on Windows only; CI will show whether Linux and macOS give the same text (http-serve-and-fetch-from-an-agent claims it unmarked too).
