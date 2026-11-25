<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-wait-for-mail-from-an-app-process
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet.
2. The app is the curl CLI started with Process.Start from PATH. The astray case waits out its 10 s bound.
3. WaitForEventAsync without RecordExchanges throws synchronously (not in the returned task); the program catches it at the call.
