<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-verify-a-download-checksum

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. Neither program checks content; the check is a POSIX sh script (`sha256sum`, else `shasum -a 256`), stated in the Solution.
2. Refusal cases rely on case order (a case-level fixture changes the served file after the kept download).
