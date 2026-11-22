<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-require-a-login-for-tools
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 10 of 10 cases pass on windows.
## Findings
1. A token is an account with an empty user name (--user :agent-token-1); --oauth2-bearer sends it. A second account alice:secret shows that -u is not an MCP login (400, exit 8).
2. Over plain mcp the right token is 401 (refused unchecked), not 403 as HTTP Basic is on a default http server (http-auth.md); --allow-plaintext-auth makes it checked and accepted. Recorded in the mcp facts.
3. Windows: --cacert alone verifies mcps (no --ssl-no-revoke needed, unlike natss).
