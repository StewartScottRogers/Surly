<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-through-a-busy-server
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 8 cases pass on windows.
## Findings
1. Distinct from http-retry-a-flaky-endpoint: exact-enough retries (--retry 2 --retry-delay 1), path and count scoping, a timing range from a short sh script (needs a POSIX shell). curl %{time_total} covers only the last attempt, so the script times the whole command.
2. With --retry-max-time 1 and --retry-delay 3 the port still made one retry (503 1); upstream curl would not. Probed, not claimed.
3. The waits-seconds case timed out once under heavy load (40 s limit) and passed on rerun; timeout kept at 40.
