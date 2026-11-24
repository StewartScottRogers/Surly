<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-the-client-that-misbehaved-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0 / Curl.Api 0.2.0 from the p7 feed; windows only.

## Findings

1. curl -H applies to every URL on the line: the bad request is put after --next (which kept the connection: one exchange per call).
