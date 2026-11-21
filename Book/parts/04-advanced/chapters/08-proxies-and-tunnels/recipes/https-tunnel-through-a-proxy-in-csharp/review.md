<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-tunnel-through-a-proxy-in-csharp

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.

## Findings

1. Carries the console claims, plus: the origin's decrypted bytes start with the HTTP/2 preface (ALPN through the tunnel), and trusting the origin does not trust an https proxy (CurlTlsOptions.Origin vs Proxy).
2. Program finding: Surl.Api refuses a server whose only listeners are --proxy in AdditionalArguments (ArgumentException: ListenUrls names no listen URL), though surl itself accepts a proxy-only command line. The recipe puts the origin in ListenUrls.
