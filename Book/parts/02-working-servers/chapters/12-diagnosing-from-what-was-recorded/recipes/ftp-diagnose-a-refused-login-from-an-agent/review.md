<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-diagnose-a-refused-login-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. A clear password is refused before the check: no login_judged event, reply 530 Login needs TLS first; a wrong one over TLS has login_judged is_accepted false and 530 Login incorrect. Right and wrong clear passwords get the same answer.
3. get_exchanges keeps PASS as sent; the program prints only its length.
