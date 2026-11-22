<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-make-and-remove-folders-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 cases pass on windows.
## Findings
1. `CurlFtpOptions.Quote` throws `QuoteError` 21 at once; replies are read from `server.Exchanges[n].Sent` with a bounded wait.
2. Fewer cases than the console recipe: its state checks (made on disk, gone from the listing) are folded into each case's `store:` text.
