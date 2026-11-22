<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-test-a-session-that-expires
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. app.sh stands in for the app; session.sh seeds EX 6 and polls the app once a second, at most fifteen times, instead of sleeping past the expiry.
2. The plaintext loosening has its refusal on a second server (WRONGPASS, 67).
