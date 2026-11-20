<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-accept-basic-only-over-https-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. One program, seven fetches; the cases match lines of its output. The certificate comes from `setup:`; `--cert` and `--key` take names relative to `SURL_MCP_ROOT`, as does `--cacert` to `CURL_MCP_ROOT`.
2. curl-mcp's `transfer` tool has no user or CA option (only `insecure`), so the logins go through the `curl` tool, with curl's own arguments.
3. The start-up warning is cited to the README, not cased.
