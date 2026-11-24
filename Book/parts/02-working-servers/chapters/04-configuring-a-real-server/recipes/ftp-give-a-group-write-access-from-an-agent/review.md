<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-give-a-group-write-access-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The table is drawn from curl exit codes over FTP (25 reader write, 9 outsider, 67 no login) and HTTP codes (403, 404/409, 401); the write probe leaves from-<user>.txt in the share.
3. A bad --share-access member makes start_server a tool error carrying surl's message.
