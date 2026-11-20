<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-read-a-files-size-and-date
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.
## Findings
1. Fixture time is set with setup: touch -t, which is local time; the claim allows the day to be 1, 2 or 3 January 2024. The -R claim compares the copy's time with a marker file written later, via an sh fixture (needs a POSIX shell).
2. Program finding: curl -w '%{filetime}' is rejected as an unknown --write-out variable by this curl port; not used.
3. RFC 3659 (MDTM, SIZE) is not in the bibliography; RFC 959 cited.
