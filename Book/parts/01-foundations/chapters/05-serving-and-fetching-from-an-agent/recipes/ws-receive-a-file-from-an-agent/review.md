<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-receive-a-file-from-an-agent
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Compares the curl tool's standardOutput with read_file like for like: text for UTF-8, base64 (isBase64 true) for binary.
2. get_exchanges first base64 chunk is the frame 0x82 0x10 + 16 bytes: one final binary frame.
3. A missing path: curl exit 22, tool error, standardOutput empty.
4. Bounded wait before reading the server's record (CI race sweep).
