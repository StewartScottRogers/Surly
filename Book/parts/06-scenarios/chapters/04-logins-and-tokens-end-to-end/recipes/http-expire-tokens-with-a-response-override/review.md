<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-expire-tokens-with-a-response-override
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, Libcurl 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. surl issues no tokens on http(s); token.txt stands in for the endpoint and the token stays valid on the server, stated in the prose.
3. Observation (not filed as a bug): an override's ProtocolError("401") carries no WWW-Authenticate challenge, unlike the server's own 401; stated and shown as a case.
