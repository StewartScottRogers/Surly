<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-fail-a-test-on-a-wrong-recipient-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. SmtpClient differs from curl on a mixed recipient list: it throws SmtpFailedRecipientException for the stray address but still delivers to the accepted one (case mixed-still-delivers). Stated as a difference, with the console twin linked.
2. The #:package Curl.Api line stays as scaffolded though this program uses only Surl.Api and System.Net.Mail.
