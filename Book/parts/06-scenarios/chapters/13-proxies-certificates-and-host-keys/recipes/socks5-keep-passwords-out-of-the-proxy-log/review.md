<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-keep-passwords-out-of-the-proxy-log
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Surl BL-1697 confirmed: the SOCKS5 relay's -v log and --trace-ascii mask PASS and Authorization credentials as ****; USER and request lines stay as sent.
3. Proxy in its own surl so its log holds only relayed exchanges; strict service uses its own folder (a folder is locked per surl, exit 124).
