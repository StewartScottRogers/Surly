<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-fail-delivery-for-one-recipient
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The fault's `path` key is the recipient address on an SMTP RCPT (`path=bob@example.com`), not in the fact sheets before; added to mail.md.
2. SmtpClient with a 451 for one of three recipients abandons the whole message (plain SmtpException, nobody receives it), unlike a 550, which delivers to the rest; both cased.
3. The .NET exception wording is the runtime's and may change between versions.
