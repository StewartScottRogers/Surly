<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-require-a-client-certificate
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 8 of 8 cases pass on windows; 1 case(s) for Linux and macOS only, not run here.
## Findings
1. Client certificate is presented as P12 (--cert file.p12:pass --cert-type P12) because Windows curl, as the Schannel build, refuses PEM client certs (exit 58 'Failed to import cert file'). The PEM case is [windows] 58 and [linux, macos] 0; the latter is expected from the OpenSSL build and not yet seen in CI.
2. curl's exit code for a refused client certificate is 55 (TLS 1.3) or 35 (TLS 1.2) on Windows and unknown elsewhere, so the cases use '|| echo refused' and the server's log line (.NET's 'The remote certificate was rejected by the provided RemoteCertificateValidationCallback'; expected the same on Linux and macOS, CI checks).
3. Checked that nothing landed in Cert:\CurrentUser\CA, Root or My after the runs.
