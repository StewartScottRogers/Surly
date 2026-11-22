<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: coaps-serve-over-dtls-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 9 of 9 cases pass on windows.
## Findings
1. Curl.Api CoAP: StatusCode 0; the response code is Headers[0].Key (`CoAP/1.0 2.05`, value empty). 4.01 is an ordinary response. CurlClientOptions.Timeout 10 s gives OperationTimedOut (28) for plain coap to the coaps port. PSK-only server: SslConnectError (35).
