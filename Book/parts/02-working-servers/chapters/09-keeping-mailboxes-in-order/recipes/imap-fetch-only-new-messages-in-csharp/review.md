<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-fetch-only-new-messages-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 of 9, with dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0 cases pass on windows.
## Findings
1. Which UIDs were fetched is read from RecordExchanges (UID FETCH lines) after every started exchange has ended (WaitForEventAsync, 10 s each).
2. CustomRequest sends STATUS now; UIDVALIDITY equals SurlMailboxInfo.UidValidity.
