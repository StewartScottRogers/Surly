<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-keep-the-server-log-for-a-failed-run-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. The Log writer withholds FTP PASS (`PASS ****`) and HTTP credentials, so the test keeps the log whole; a case shows the masked PASS line, another that no secret is left.
2. SelfSigned is a loosening without its own refusal case here; the client pins the certificate (CertificateAuthorityBlob), as in http-exchange-a-password-for-a-token-in-csharp.
3. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: surl withholds passwords from its Log; the line filter and full.log are gone, and the LDAP exception is left to the console twin.
