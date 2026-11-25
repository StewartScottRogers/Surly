<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-trust-a-test-ca-end-to-end-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. CertificateAuthorityFile is --cacert, SslOptions.NoRevoke is --ssl-no-revoke; VerifyPeer and VerifyHost false together are -k (shown, never used for the real servers).
3. The revocation case is [windows]; a null CertificateAuthorityFile falls back to the system store, which does not hold the test CA on any platform.
