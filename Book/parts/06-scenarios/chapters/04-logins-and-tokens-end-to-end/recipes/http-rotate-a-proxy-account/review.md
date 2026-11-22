<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-proxy-account

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.

## Findings

1. remove_proxy_account of the last proxy account makes the proxy anonymous (forwards with no login, 200) and answers with no warning, unlike remove_account's `no account left` warning. Reported as an inconsistency.
2. Not a recipe claim but found while probing: on a plain http proxy listener, a Basic proxy login (refused 403 unchecked) is written to the -v log as `Proxy-Authorization: Basic <base64 of user:password>`, not withheld (ADR-0182 lists HTTP proxy as withheld). Reported.
3. Login events for proxy logins carry scheme `http`, the same as origin logins; the recipe's origin has no accounts so the events are the proxy's.
Re-pinned to Surl a423807a5d / Curl 0680b97577: remove_proxy_account of the last proxy account now answers a warning that the proxy needs no login (BL-1479); last-account-opens-the-proxy and the Discussion restated.
