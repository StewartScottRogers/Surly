<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-move-a-firmware-fetch-to-http

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. The images are random bytes made by `setup:` (`head -c`), so the comparison is not against a fixture; `cmp` is the test. Needs `head`, `cmp` and `&&`, all present with Git on Windows.
2. One surl serves tftp and http on the same port number (UDP and TCP are separate); `ready: port` waits on the TCP side.
3. Chapter intro for 06/07 was written by another writer and talks of turning the old protocol off; this recipe does not do that. Lead may want to widen it.
