<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-share-a-folder-between-mailboxes-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet.
2. Curl.Api APPEND (PUT with FromStream and length) works at Curl 2145a49eae, so no raw IMAP is needed, unlike the older twin.
3. A refused SELECT is CurlException code 67, named LoginDenied by Curl.Api, message Select failed; the prose says the login itself worked.
