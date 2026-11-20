<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-wait-for-an-apps-background-work
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 3 of 3 cases pass on windows.
## Findings
1. Two WaitForEventAsync calls awaited with Task.WhenAll; timing claimed as a range (>= the app's 1 s delay, < 30 s timeout) after one untimed warm-up request.
2. Found while writing: a PUT into a folder that does not exist is 409 Conflict (the receipt now goes at the root); recorded in facts/http.md, not claimed.
3. The app's mailbox needs ;protocols=smtp,http to log in over HTTP.
