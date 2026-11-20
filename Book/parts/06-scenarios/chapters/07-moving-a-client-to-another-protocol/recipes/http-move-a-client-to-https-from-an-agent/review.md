<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-a-client-to-https-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. `get_exchanges` `scheme` filter is the proof; the moved client is the `curl` tool because `transfer` has no `--proto` or `--cacert`.
