<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-require-a-login-for-tools-in-csharp
Verdict: accept with findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 10 of 10 cases pass on windows.
## Findings
1. Curl.Api 0.2.0 sends no tools/call over MCP (Body ignored; see the stdio C# review), so this twin checks the login gate on the tool list only; the console's call-with-login claim has no C# case. The limitation is claimed (body-is-not-sent).
2. CurlAuthenticationOptions.XOAuth2Bearer is --oauth2-bearer; refusals are CurlException LoginDenied 67, WeirdServerReply 8 (password), PeerFailedVerification 60.
