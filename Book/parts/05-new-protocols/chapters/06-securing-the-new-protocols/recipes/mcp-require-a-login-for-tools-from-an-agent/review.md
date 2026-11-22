<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-require-a-login-for-tools-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 11 of 11 cases pass on windows.
## Findings
1. curl-mcp transfer headers must be a JSON array of {name, value} objects; passing a C# Dictionary<string,string>[] through the MCP SDK throws NotSupportedException (no JsonTypeInfo), so the program passes a parsed JsonElement.
2. transfer with caFile and an Authorization: Bearer header lists the tools (statusCode 0 for MCP).
