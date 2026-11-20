<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-route-mail-through-aliases-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The test reads the store with Surl.Api (no IMAP client), so IMAP is only implied; the console twin checks over IMAP.
2. The `\Seen` remark is the API's documented behaviour, cited to RFC 9051 for what an IMAP fetch does.
