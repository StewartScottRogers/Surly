<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-dead-server-from-a-missing-file

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 cases pass on windows.

## Findings

1. Exit 7 here is TCP, the same on every platform; no platforms: needed (the UDP differences in facts/platforms.md do not apply).
2. The stopped case runs a short sh script (needs a POSIX shell). Its surl cannot use `site`, which the main server's lock holds, so it serves a copy.
3. Exit 6 uses `no-such-host.invalid`, which depends on a resolver answering NXDOMAIN rather than hanging; it did not hang here.
