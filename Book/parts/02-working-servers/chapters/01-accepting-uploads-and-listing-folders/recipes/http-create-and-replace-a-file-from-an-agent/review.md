<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-create-and-replace-a-file-from-an-agent
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Uploads go through the curl tool (curl -T with --digest -u), because transfer has no login field; -T files are fixtures in CURL_MCP_ROOT.
2. read_file on a missing path is a tool error with text 'No file at /archive/report.txt.'.
