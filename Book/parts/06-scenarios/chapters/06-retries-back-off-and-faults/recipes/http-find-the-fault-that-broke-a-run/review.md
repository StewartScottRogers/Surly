<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-the-fault-that-broke-a-run
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Found: under a command-stage delay the server still writes its 200 OK after the client has given up (Windows); a server-side-only scan misses the timeout. The case accepts '200 OK' or nothing for other platforms.
2. Matching is by request line in call order; one curl tool call per request gives one exchange each; an untimed warm-up transfer is exchange 1.
3. SurlFaultRule item text percent-encodes path= (%2F) and omits times (also in surl-count-the-faults-that-fired).
