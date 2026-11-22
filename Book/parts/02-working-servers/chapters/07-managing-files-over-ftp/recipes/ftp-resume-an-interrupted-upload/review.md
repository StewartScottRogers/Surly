<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-an-interrupted-upload
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 13 cases pass on windows.
## Findings
1. `curl -C - -T` sends `SIZE` then `APPE` (no REST, no STOR); with no remote file `SIZE` is 550 then `STOR`.
2. curl never compares bytes: a zero-filled partial of the right length is resumed and the result differs (claimed on purpose).
3. Explicit `-C 100000` sends `APPE` with no `SIZE`.
