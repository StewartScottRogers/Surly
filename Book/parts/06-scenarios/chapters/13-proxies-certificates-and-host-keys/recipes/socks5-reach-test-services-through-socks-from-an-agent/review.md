<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-reach-test-services-through-socks-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 2 of 2 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. get_exchanges on the proxy listen URL: the second received chunk is the SOCKS5 request; the program decodes name and port. Exchange ids are not printed (they interleave with the origins').
3. The passive port comes from the FTP listener's own get_exchanges (229 reply chunk).
