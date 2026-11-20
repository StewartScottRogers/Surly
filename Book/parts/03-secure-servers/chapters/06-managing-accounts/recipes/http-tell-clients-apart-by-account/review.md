<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-clients-apart-by-account

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. Only -v names accounts; the info-level case shows why the recipe needs it.
2. grep runs under cmd.exe through Git's PATH; the Solution says grep needs a POSIX shell.
3. Request lines carry the disk path; matched with [^
]* so Linux and macOS paths pass too.
