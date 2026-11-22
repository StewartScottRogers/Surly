<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-search-by-sender-and-date-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 12 of 12 cases pass on windows.
## Findings
1. Searches are CurlRequest URLs with a query; UID SEARCH and the expunge go through CustomRequest. The exception for a bad date carries code 21 only, not the BAD line.
2. Same twelve claims as the console recipe.
