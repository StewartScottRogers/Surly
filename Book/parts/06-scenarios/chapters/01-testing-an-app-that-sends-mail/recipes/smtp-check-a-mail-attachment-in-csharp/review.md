<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-a-mail-attachment-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. The app is SmtpClient with an Attachment (needs AllowPlaintextAuthentication; case smtpclient-needs-plaintext shows the refusal without it). SmtpClient puts the file name in Content-Type name=, its Content-Disposition is a bare "attachment", so the test checks Content-Type.
2. SmtpClient's exception text is .NET's wording, checked exactly; it may change with .NET versions.
