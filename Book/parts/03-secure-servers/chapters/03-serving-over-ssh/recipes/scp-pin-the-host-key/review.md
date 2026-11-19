<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: scp-pin-the-host-key
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.
## Findings
1. The pin is computed, not pasted: setup writes `ssh-keygen -lf host_key.pub > fingerprint.txt`, and curl reads the hash itself with `--variable "host_sha256[12-54]@fingerprint.txt"` and `--expand-hostpubsha256 "{{host_sha256}}"` (curl 8.12 byte ranges). Same text in sh, cmd and PowerShell; the case `fingerprint-shape` proves the offsets. The variable name has an underscore so the book's `{{port}}` substitution leaves it alone.
2. Needs the author: run.ps1's `>` writes UTF-16 under Windows PowerShell 5.1, which would break the byte range; it is fine under PowerShell 7 (UTF-8) and in sh and cmd. If run.ps1 must support 5.1, the build should run setup lines through `cmd /c` or the recipe needs another way.
3. Truth (5): the `surl -v` line `* Serving SSH host key ... --hostpubsha256` and "curl on Windows offers only RSA host-key algorithms" are cited from the Surl README, not cases (server log; platform-specific). `-v` stays in the server command for the reader, not the test (Fitness 3).
4. `--hostpubmd5` left out: ssh-keygen prints MD5 as colon-separated hex, which curl does not take, so no setup line computes it portably.
5. The title names Secure Shell though the key says scp; the sftp case shows the one key serves both.
