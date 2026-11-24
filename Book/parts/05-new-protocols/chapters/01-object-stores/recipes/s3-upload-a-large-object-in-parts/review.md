<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-upload-a-large-object-in-parts
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Multipart over TLS with SigV4 https; ~2 min to verify (three uploads of 11-13 MB through TLS).
3. --max-filesize counts the whole upload: the crossing part is 400 EntityTooLarge and the upload is dropped (abort 404). A single part over the limit leaves the upload open (abort 204): inconsistent, noted for Surl, not claimed.
