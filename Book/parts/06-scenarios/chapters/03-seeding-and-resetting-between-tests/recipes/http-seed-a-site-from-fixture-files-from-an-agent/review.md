<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-seed-a-site-from-fixture-files-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.

## Findings

1. The on-disk case writes the case folder's fixtures/users.json and runs last; reordering cases would break later ones.
