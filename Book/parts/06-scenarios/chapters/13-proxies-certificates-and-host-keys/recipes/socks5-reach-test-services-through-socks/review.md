<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-reach-test-services-through-socks
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The FTP data connection goes through the SOCKS proxy too: the case matches the 229 reply's port to the third Connected to line with a backreference, two greps so no log-order race.
3. FINDING (Surl, reported to lead): the proxy's -v log writes relayed bytes in clear, FTP PASS and HTTP Authorization, while the origin's own exchange withholds them as ****. Not claimed.
