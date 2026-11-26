<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-a-duplicate-from-a-lost-reply-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp, curl-mcp from the same builds: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet.
2. In-process there is no port check, so the after-bytes rule goes in SurlServerOptions.Faults at start.
3. Curl.Api has no retry; the sender catches CurlException (RecvError on the graceful cut) and sends once more. CurlClientOptions.Timeout 10 s bounds each try.
