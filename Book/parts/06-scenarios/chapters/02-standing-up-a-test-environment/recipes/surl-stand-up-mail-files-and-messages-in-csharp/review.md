<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-stand-up-mail-files-and-messages-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The IMAP listener is started but the test reads the mail store through Surl.Api; the console twin reads over IMAP.
2. Bounded wait before reading the server's record (CI race sweep).
