<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-upload-several-files-at-once-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Nearest honest form: curl-mcp's curl tool refuses -T with glob characters (it cannot check expanded paths against CURL_MCP_ROOT), so the agent passes one -T and URL per file; the refusal is case no-glob.
Re-pinned to Surl a423807a5d / Curl 0680b97577: curl-mcp now takes -T globs, checking each expansion against CURL_MCP_ROOT, so no-glob became glob (uploads) and glob-outside (refused); surl's 401 now carries a one-line body (BL-1444).
