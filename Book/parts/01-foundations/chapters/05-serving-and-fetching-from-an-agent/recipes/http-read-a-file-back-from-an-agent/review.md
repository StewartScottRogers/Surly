<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-read-a-file-back-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. read_file answers text for valid UTF-8 and base64 otherwise; transfer likewise sets isBase64. The program compares bytes, not strings, so the comparison holds either way.
2. Removed two draft sentences (read_file reads the store whatever a client got; the type comes from the name): no case here shows them. The C# recipe http-serve-files-from-memory carries the second.
