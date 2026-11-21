<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-check-unread-counts-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. The planned task (send STATUS through Curl.Api) is impossible at Curl.Api 0.2.0: a request's method is one HttpMethod token, and `new HttpMethod("STATUS INBOX (MESSAGES UNSEEN)")` throws FormatException. The recipe counts with SEARCH UNSEEN (a URL query) instead, says so, and keeps the FormatException as a case. Revisit when Curl.Api gains a custom-request property (BL-5612).
2. ReadMessageAsync sets no \Seen; the program shows it, which is the point for a test that checks a badge.
3. Widened to the console claims Curl.Api can show. Finding: a CurlClient that keeps its connection with INBOX selected does not see mail delivered after it selected (SEARCH still covers 1 2 3; a new client sees 3 4): surl sends no untagged EXISTS to the selected session. Claimed as observed; worth a Surl note.
