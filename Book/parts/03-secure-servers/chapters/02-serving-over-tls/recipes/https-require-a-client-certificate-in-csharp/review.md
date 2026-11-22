<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-require-a-client-certificate-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 8 of 8 cases pass on windows; 1 case(s) for Linux and macOS only, not run here.
## Findings
1. CurlException Code for the refusal varies (SslConnectError/SendError), so cases match any code; the PEM case is SslCertProblem on Windows.
2. SurlServerOptions.CaCertificateFile is --cacert; start refusals FailedInit (2) and CaCertificateBadFile (77).
- Lead: CI on Linux showed .NET's refusal text there is "Authentication failed, see inner exception."; the log claim is split into an all-platform handshake failure and a Windows/macOS case naming the rejected certificate.
