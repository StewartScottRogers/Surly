<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-require-a-socks-login-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. Same claims via Surl.Api Log; ProxyUserName/ProxyPassword stand in for --proxy-user.
