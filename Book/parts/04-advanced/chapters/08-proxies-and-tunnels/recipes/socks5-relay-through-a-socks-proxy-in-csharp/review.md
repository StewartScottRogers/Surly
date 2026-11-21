<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-relay-through-a-socks-proxy-in-csharp

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.

## Findings

1. Carries the console's eleven claims in eight cases (each refusal prints the CurlException and the server's log line together); the log also names RefusedPlaintext for the rejected clear login.
2. CurlNetworkOptions.Resolve is curl --resolve; Timeout bounds the refusal cases.
