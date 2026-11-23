<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fail-a-proxy-tunnel-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. Curl.Api has no http_connect value on a failed transfer: the twin shows the difference as exception (proxy) versus CurlResponse 503 (origin).
2. One server holds origin and proxy; the origin-503 rule is scoped scheme=http path=/busy.txt.
