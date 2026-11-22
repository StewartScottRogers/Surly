<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-check-the-flags-an-app-left-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 of 9, with dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0 cases pass on windows.
## Findings
1. Curl.Api now has CurlRequest.CustomRequest (STORE, EXPUNGE, UID FETCH work); facts/mail.md said it could not.
2. Surl.Api SurlMessageSummary.Flags orders \Seen before \Deleted, the IMAP FETCH answer the other way round; stated.
3. ReadMessageAsync sets no flag; kept as a C#-only case.
