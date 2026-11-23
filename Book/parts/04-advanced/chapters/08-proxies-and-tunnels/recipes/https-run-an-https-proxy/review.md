<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-run-an-https-proxy

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. Two certificates (proxy.pem, origin.pem) so trust is visibly separate, unlike https-tunnel-through-a-proxy which shares one.
2. 60 matched on 'curl: (60)' only (TLS library wording).
3. No certificate store touched.
