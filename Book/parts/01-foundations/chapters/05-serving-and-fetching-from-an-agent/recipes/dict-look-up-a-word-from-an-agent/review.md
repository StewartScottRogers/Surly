<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dict-look-up-a-word-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Uses the curl tool as the task asks; transfer also reaches dict:// (statusCode 0), noted in facts/dict.md, not claimed.
2. Dot files and folders as non-headwords are left to the console recipe dict-look-up-a-word.
