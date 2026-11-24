<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-resume-only-if-unchanged-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0 / Curl.Api 0.2.0 from the p7 feed; windows only.

## Findings

1. transfer with Range and If-Range headers; the curl tool's -C - would discard the 200 body (exit 33), so it is not used.
