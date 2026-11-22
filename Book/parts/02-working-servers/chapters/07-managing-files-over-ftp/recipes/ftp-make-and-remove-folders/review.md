<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-make-and-remove-folders
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 17 cases pass on windows.
## Findings
1. `-Q` rides on a listing, so the console recipe needs `--list-directories`; `MKD` and `RMD` replies and the exit 21 behaviour are probed.
2. `MKD` makes no parent (550 No such directory), unlike `--ftp-create-dirs`; `RMD` of a file is 550 No such directory, of a full folder 550 Directory not empty.
3. The empty folder on the read-only server is made by `setup: cd other && mkdir empty` (a fixture cannot hold one; `mkdir other/empty` fails under cmd).
