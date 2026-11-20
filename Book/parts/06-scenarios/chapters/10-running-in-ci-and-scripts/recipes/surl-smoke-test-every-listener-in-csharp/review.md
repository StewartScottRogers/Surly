<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-smoke-test-every-listener-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Program finding: Curl.Api custom request NOOP over SMTP gives StatusCode 0 where curl -X NOOP -w %{response_code} prints 250 (expected 250, as curl). Recipe states 0 as observed.
2. No-answer is CouldntConnect or OperationTimedOut; other CurlException codes (LoginDenied for FTP) count as an answer.
