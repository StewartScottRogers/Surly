<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-refuse-a-second-server-on-one-folder
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.
## Findings
1. Overlaps surl-read-the-exit-codes, whose solution is the same refusal; this one adds what the reader does next, each as a case: one surl with two listen URLs, another folder alongside, and the lock holding across protocol and spelling.
2. Cases that start a surl expected to refuse use port 0 with `timeout: 10`, so a regression that let it serve fails instead of hanging verify.
3. "The lock is released when the process ends, even when killed" and "the lock file stays" are cited to <<surl-readme>>; verify cannot kill a server mid-run.
4. Not claimed: a `--share` naming the held folder in a second surl is not refused (program finding in the chapter report).
