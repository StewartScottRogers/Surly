<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-several-files-at-once-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.
## Findings
1. Connections counted from get_events; no timing claims, since curl-mcp set-up adds 2 s or more to every call.
2. curl tool writes with -o got/f#1.txt --create-dirs under CURL_MCP_ROOT; the program checks every file.
