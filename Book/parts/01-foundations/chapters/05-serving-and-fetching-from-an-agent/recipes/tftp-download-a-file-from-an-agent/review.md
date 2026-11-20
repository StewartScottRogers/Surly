<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-download-a-file-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Carries the console tftp-download-a-file claims plus the --allow-uploads write with read_file. TFTP exchanges are base64 packets; the read request is decoded in the program. The transfer tool reaches tftp (statusCode 0).
2. RFC 1350 cited.
