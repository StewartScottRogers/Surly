<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-an-empty-store

Verdict: accept

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 5 of 5 cases pass on windows.

## Findings

1. Truth (5): "nothing is written to disk" and "gone when the process ends" are cited to the Surl README, not cases; a case for them needs a restart, which belongs in `http-keep-files-across-restarts`.
2. Reader (7): the refusal of `DELETE` says why (a method that changes the store needs an account) and points to `<<http-accept-uploads>>` for the loosening, which is not written yet.
