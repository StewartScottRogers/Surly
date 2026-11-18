<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-hide-and-show-dot-files

Verdict: accept

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 3 of 3 cases pass on windows.

## Findings

1. The default (dot names hidden) is not a case here, because a recipe has one listener; it is a case in `<<http-serve-a-directory>>`, which the Discussion points to.
2. The fixture `site/.env` holds a made-up value; it exists to show the option's reach, not a real secret.
