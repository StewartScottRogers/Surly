<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: xmpp-require-tls-before-login-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl.Api 1.0.0 (Surl commit 5da5b3eaf), Curl.Api (Curl commit c84035f797): 14 of 14 cases pass on windows.

## Findings

1. Carries all the console claims, and adds STARTTLS done by hand with a TcpClient and SslStream. Every raw read has a 10 s bound.
2. The Curl.Api TLS error for the untrusted certificate is shown by code only; its message is the TLS library's.
