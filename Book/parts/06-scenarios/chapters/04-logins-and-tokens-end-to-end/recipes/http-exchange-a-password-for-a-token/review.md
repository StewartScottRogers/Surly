<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-exchange-a-password-for-a-token

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.

## Findings

1. Nearest honest recipe: surl issues no tokens, so the token is a Bearer account (--user :tok) and the token endpoint is a file holding it; said in the Discussion.
2. surl does not scope accounts to paths: the password alone reaches the API (a case), so the log is the proof the client used the token.
3. Over h2, -v logs DATA frames, so the served token appears in the log as a body; no 'token not in log' claim is made.
