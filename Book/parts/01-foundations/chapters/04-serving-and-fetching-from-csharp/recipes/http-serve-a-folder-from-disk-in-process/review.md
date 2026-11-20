<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-a-folder-from-disk-in-process
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. The .surl folder holds a file named lock while the server runs; the case checks that file exists and that /.surl/lock and /.surl/ are 404. Other contents of .surl are not claimed.
2. The lock's release on stop is claimed in http-stop-a-server-and-read-its-exit-code, not here.
