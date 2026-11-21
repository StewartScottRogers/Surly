<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpcs-call-over-tls-in-csharp
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Nearest honest recipe: Curl.Api 0.2.0 sends no gRPC request message (POST body not sent, reply {}; facts/grpc.md), so the call uses an empty request; case no-request-message pins this and will break when Curl.Api sends bodies, at which point the solution should send {"message":"hello"}.
2. Untrusted and wrong host name: CurlException PeerFailedVerification (60); Message is schannel wording on Windows, cases match the first line only.
3. No certificate: SurlServerStartException ExitCode CertificateProblem (58).
