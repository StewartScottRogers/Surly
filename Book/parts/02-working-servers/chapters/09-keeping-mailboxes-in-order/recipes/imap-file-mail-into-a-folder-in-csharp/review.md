<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-file-mail-into-a-folder-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 9 of 9 cases pass on windows.
## Findings
1. Curl.Api 0.2.0 sends STORE, COPY, MOVE, EXPUNGE and STATUS through CurlRequest.CustomRequest (one token no longer needed); a refusal is CurlException code 21 without the server's NO text, so the store is the check.
2. Same nine claims as the console recipe; one program, one mode per case, each case starts its own server.
