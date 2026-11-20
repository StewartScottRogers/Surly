<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-the-mail-an-app-sent-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Program finding (corrected by the lead): Curl.Api 0.2.0 sends SMTP only with a stream body; a byte[] body is POSTFIELDS, so Put sends a literal `PUT` (500, exit 8) and Post does a VRFY (252). SmtpClient plays the app, as a .NET app under test would. The app is therefore System.Net.Mail.SmtpClient, which is also what a .NET app would use.
2. SmtpClient has no CRAM-MD5, the only mechanism surl offers without TLS, so the server needs AllowPlaintextAuthentication (a loosening); case smtpclient-needs-plaintext shows the refusal without it. The warning on start is cited, not cased.
3. SmtpClient's exception text (MustIssueStartTlsFirst, 'Mailbox unavailable') is .NET's wording, checked exactly; it may change with .NET versions.
