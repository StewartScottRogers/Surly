<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-a-flaky-endpoint
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 10 of 10 cases pass on windows.
## Findings
1. Each failure path has its own server and folder, since each case spends the times=2 rule.
2. The server-side check is surl's verbose log in a file (-v --log-file surl.log) read with grep -c, which verify runs under cmd.exe through Git's grep on Windows.
3. The curl port's retry notice reads 'Retrying in 1 second.'; upstream curl's wording may differ ('Will retry in'). Claims match the port's text.
4. No Retry-After header on surl's 503, so curl's own back-off (1 s, 2 s) is what the stderr lines show.
