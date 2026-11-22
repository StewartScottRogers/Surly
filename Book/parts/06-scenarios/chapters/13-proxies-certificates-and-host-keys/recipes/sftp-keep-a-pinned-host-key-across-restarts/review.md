<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-keep-a-pinned-host-key-across-restarts
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 8 of 8 cases pass on windows.
## Findings
1. The pin is written by pin.sh in setup: to a curl config file (-K pin.cfg) so the run lines stay portable; restart: is used twice on the same server, and a -v --log-file shows the same fingerprint at all three starts.
2. Without -v the log has no login lines, so the no-login claim uses the verbose log ('SSH negotiated kex' present, 'login request' absent).
