<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-list-a-container

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. Decision: the prefix is part of the URL path (`.../photos/2026/`); curl drops a query string, so `?prefix=` is not a spelling and is not claimed.
2. Left out: `--list-directories` also answers Data Lake listings and List Containers; `az://host/account/` alone is refused by curl (exit 3) and is not claimed.
