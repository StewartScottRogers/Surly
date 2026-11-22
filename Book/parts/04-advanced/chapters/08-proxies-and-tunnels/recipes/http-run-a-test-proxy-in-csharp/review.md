<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-a-test-proxy-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.

## Findings

1. SurlServerOptions has no proxy property; `AdditionalArguments` carries `--proxy`, stated in the Discussion.
2. Allowed-origin case accepts 504 or 502, as on the console.
3. Bounded wait before reading the server's record (CI race sweep).
