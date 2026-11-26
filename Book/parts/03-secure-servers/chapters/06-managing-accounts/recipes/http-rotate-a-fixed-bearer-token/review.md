<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-fixed-bearer-token
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Removing the token, the only account, reopens the server (no token 200); the old token is still refused 401. Both are cases.
3. FINDING (Surl, minor): a second POST /v1/accounts with user "" answers 409 with message "user  already has an account" (empty name, two spaces); surl-mcp says "the Bearer-token account already exists". Claim matches the message loosely.
