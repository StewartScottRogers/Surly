<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-require-a-login-for-tools-in-csharp
Verdict: accept with findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 10 of 10 cases pass on windows.
## Findings
1. Curl.Api 0.2.0 sends no tools/call over MCP (Body ignored; see the stdio C# review), so this twin checks the login gate on the tool list only; the console's call-with-login claim has no C# case. The limitation is claimed (body-is-not-sent).
2. CurlAuthenticationOptions.XOAuth2Bearer is --oauth2-bearer; refusals are CurlException LoginDenied 67, WeirdServerReply 8 (password), PeerFailedVerification 60.
- Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: Curl.Api now sends the Body as a tools/call (BL-5758), so body-is-not-sent became call-with-login (read_file through the login, the console's claim) and call-without-login (LoginDenied 67); finding 1 is fixed. 11 of 11 cases pass on windows.
