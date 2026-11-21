<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-resume-an-interrupted-download-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. No program finding. The curl tool runs -C - on a file under CURL_MCP_ROOT as curl does; a failed If-Range is exit 33, reported as a tool error with the -w output still in standardOutput.
2. The request line is read back with get_exchanges, which is how the recipe shows the Range the server saw.
3. Re-pinned to Surl a423807a5d / Curl 0680b97577: the 416 now has a 26-byte body that size_download counts; curl still does not append it.
