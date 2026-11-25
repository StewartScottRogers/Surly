<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-share-connections-between-handles
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, Libcurl 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Three servers, one per mode, so each -v log holds one run; the log claims use a regex backreference on the exchange number, since verify's readiness probe takes #1.
3. Cookie given with CURLOPT_COOKIELIST (surl sets no cookies); without Domain= no cookie was sent at all, which is not claimed.
