<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: coaps-serve-over-dtls
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 11 of 11 cases pass on windows.
## Findings
1. With an account, coaps reads from a certificate client are 4.01 too (curl cannot send a pre-shared key), so the Solution's server has a certificate and no account; the account's effect is shown on separate servers. An account with no certificate is PSK-only: curl exit 35 `DTLS handshake failed: the server sent the fatal alert HandshakeFailure` (matched as `curl: (35)`).
2. Plain coap to the coaps port: no answer; -m 10 gives exit 28. Every curl has -m 10. The 58 case uses --directory config, which the main server also serves; the certificate check comes before the directory lock.
3. The DTLS code is the port's own on both sides, so no platform split is expected; Linux and macOS not run locally.
