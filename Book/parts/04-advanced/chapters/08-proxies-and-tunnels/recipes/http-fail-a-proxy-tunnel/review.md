<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fail-a-proxy-tunnel

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. Distinct from https-tunnel-through-a-proxy (one refusal case there): here forwarding beside the refused CONNECT, %{http_connect} vs %{http_code}, an origin 503 for contrast, -p, the rule in the proxy log and the origin's empty TLS record.
2. The origin's log is checked for no 'TLS handshake completed' (verify's readiness probe adds a failed handshake, not a completed one).
