<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-seed-a-site-from-fixture-files

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.

## Findings

1. setup: copies fixtures to run with rm and cp, which run under cmd.exe through Git's PATH.
2. Both loosening options have refusal cases on their own servers (strict, no-uploads).
