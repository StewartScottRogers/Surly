<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-keep-the-server-log-for-a-failed-run-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. Same nearest honest recipe as the console twin: the verbose Log holds the FTP PASS line, so the test drops lines starting '#<n> < ' before keeping it (cases show the full log has the password, the kept one has none).
2. SelfSigned is a loosening without its own refusal case here; the client pins the certificate (CertificateAuthorityBlob), as in http-exchange-a-password-for-a-token-in-csharp.
3. run: grep cases check the kept files the program wrote; full.log is written only so a case can show what the unfiltered log holds.
