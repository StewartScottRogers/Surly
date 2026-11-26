<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-fixed-bearer-token-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Refusals printed by exception type only; the Add message is "User  already has an account." (same empty-name wording finding as the console recipe).
3. Every token login is judged as account "bearer token", Mechanism Bearer; neither token appears in the log.
