<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-survive-a-temporary-rcpt-failure-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The app is System.Net.Mail.SmtpClient (needs AllowPlaintextAuthentication; the refusal without it is cased).
2. .NET detail checked exactly: a 451 on the only RCPT is a plain SmtpException (LocalErrorInProcessing), a 550 an SmtpFailedRecipientException; the wording may change with .NET versions.
3. FindEvents needs RecordExchanges = true (InvalidOperationException otherwise); the program sets it.
