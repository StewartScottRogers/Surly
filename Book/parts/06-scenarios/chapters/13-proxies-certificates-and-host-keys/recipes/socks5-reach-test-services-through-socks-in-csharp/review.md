<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-reach-test-services-through-socks-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 2 of 2 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The log is read after StopAsync; each Connected to line is named by port, the passive port taken from the 229 line of the same log.
3. The relay log finding of the console twin probably applies to Surl.Api Log too (not probed); not claimed.
