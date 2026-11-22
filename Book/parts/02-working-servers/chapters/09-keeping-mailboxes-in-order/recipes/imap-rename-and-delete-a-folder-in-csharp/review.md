<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-rename-and-delete-a-folder-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 12 of 12 cases pass on windows.
## Findings
1. Curl.Api 0.2.0 still cannot APPEND (Put with bytes or a stream with a length: CurlException 25 Cannot APPEND with unknown input file size), so the setup files two orders into Old with MOVE 1:2 Old. Twelve cases against the console's thirteen: the delete-with-messages claim and its follow-up are one case.
2. Refusals are CurlException 21 without the server's NO line; the store (ListMailboxesAsync) shows nothing changed.
