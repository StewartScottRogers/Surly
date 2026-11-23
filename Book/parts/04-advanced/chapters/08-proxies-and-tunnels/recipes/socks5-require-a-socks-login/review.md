<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-require-a-socks-login

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. The client gets the same 97 (1 1) for a clear-text refusal and a wrong password; only the -v log (RefusedPlaintext vs RefusedCredentials) tells them apart.
2. Minor: the -v log masks the password as **** but keeps the RFC 1929 length byte (e.g. \x08), so the password length shows.
