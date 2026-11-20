<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-a-flaky-endpoint-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. transfer makes one attempt; the curl tool carries --retry. The 503 rule is added with add_fault after start.
2. With -f and retries, the curl port writes 'curl: (22) ...' to stderr for each failed try, before and after the retry notice.
