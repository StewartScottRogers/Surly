<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-client-bug-from-a-fault-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0 / Curl.Api 0.2.0 from the p7 feed; windows only.

## Findings

1. drop;stage=command gives exit 56 on every platform (platforms.md); the dropped exchange has a request and no sent chunk.
