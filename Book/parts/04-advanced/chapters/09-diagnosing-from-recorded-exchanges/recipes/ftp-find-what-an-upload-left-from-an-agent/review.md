<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-find-what-an-upload-left-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. The job's success is curl's last-transfer exit code (0) hiding a 9; the agent's verdict comes from list_files, read_file and store_changed events.
