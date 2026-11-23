<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-despite-a-bad-recipient-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Finding: Curl.Api exposes no per-recipient answer; with MailRecipientAllowFails the CurlResponse is 250 and names nobody (no debug callback in Curl.Api 0.2.0). The solution therefore sends one request per recipient so each CurlException belongs to one address, and the which-refused case reads the 550 from Surl.Api's recorded exchange.
3. System.Net.Mail.SmtpClient would report FailedRecipient in one send but needs AllowPlaintextAuthentication; left out.
