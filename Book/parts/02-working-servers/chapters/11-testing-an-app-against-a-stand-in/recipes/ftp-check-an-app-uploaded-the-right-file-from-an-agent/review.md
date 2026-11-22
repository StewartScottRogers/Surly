<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-check-an-app-uploaded-the-right-file-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 7 cases pass on windows.
## Findings
1. `read_file` answers `text` and `byte_count`; the comparison is of text, which is exact for this ASCII export.
2. Refusal messages after `curl: (9)` and `curl: (67)` are matched by prefix only.
