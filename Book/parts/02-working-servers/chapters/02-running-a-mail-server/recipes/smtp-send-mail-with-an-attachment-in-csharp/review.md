<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-mail-with-an-attachment-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. CurlMultipartContent works as an SMTP body (POST) with request Headers at the top; Curl.Api then sends MAIL FROM with SIZE.
3. A CurlMimePart.Bytes part with no ContentType gets no Content-Type header at all (the CLI's file part gets application/octet-stream); claimed as observed. Plan lists imap; the program reads the store, so protocols is [smtp].
