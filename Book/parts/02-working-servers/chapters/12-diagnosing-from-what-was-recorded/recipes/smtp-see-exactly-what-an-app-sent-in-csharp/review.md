<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-see-exactly-what-an-app-sent-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The test fails on purpose (exit 1) and prints the recorded dialogue; it waits for ExchangeEnded after disposing the CurlClient (QUIT) and bounds the assertion wait at 2 s.
3. SurlExchangeRecord keeps the CRAM-MD5 answer as sent (base64); the verbose Log writes ****. Both are cases.
