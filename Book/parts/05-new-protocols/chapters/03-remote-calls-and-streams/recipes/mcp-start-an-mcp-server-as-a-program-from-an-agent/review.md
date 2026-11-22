<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-start-an-mcp-server-as-a-program-from-an-agent
Verdict: accept with findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. The agent's own MCP client (StdioClientTransport, Command "surl") starts surl with mcp+stdio:; that is the task. mcp-tools are surl's own tools plus curl-mcp's curl.
2. Program finding: surl-mcp start_server with listen_urls ["mcp+stdio:"] answers a JSON-RPC Internal error (-32603) instead of a tool error with exit code 2 and surl's message. Not claimed; the Discussion does not offer start_server for stdio.
3. curl-mcp's curl tool with an mcp+stdio URL starts surl from PATH and calls the tool (claimed). transfer with an mcp+stdio URL also starts it and lists the tools; not claimed.
