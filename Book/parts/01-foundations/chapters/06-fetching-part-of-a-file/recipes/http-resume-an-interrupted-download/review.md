<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-resume-an-interrupted-download
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. No program finding. -C - on a complete file is 416, exit 0, file untouched (as curl does); -C - with no file is a plain 200.
2. The partial file is made by a fixture, not by interrupting a transfer, which would be timing-dependent.
3. The case that the file may have changed on the server is shown with a wrong prefix, not with a changed server file.
4. If-Range is honoured by surl (a date that is not the file's Last-Modified gets 200 and the whole file); curl -C - then exits 33 and leaves the partial file.
