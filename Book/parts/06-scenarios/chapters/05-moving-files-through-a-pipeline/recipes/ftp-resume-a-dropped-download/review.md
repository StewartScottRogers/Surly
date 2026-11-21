<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-a-dropped-download
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. Nearest honest form: surl cannot drop an FTP download after a set number of file bytes (drop;after-bytes counts only control-connection bytes; reported). Setup makes the partial file a cut-off download leaves, and a drop;stage=command;command=RETR rule drops the resumed request once.
