<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-require-a-socks-login-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. No log through surl-mcp: the strict refusal shows as no login_judged event, a wrong password as is_accepted false.
2. Program finding (reported): get_exchanges keeps the SOCKS5 login message unmasked, password included, while the -v log writes asterisks. Claimed as observed, with a caution in the prose.
