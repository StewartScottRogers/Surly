<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-plan-a-move-from-recorded-traffic
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Data-connection bytes are in the FTP record between 150 and the next reply; the agent skips them.
3. Gaps found: SFTP rename onto an existing name is refused (exit 21, Operation failed; FTP RNTO replaces), and curl -I over SFTP prints no Last-Modified. Both observed, not cited; workarounds are cases.
