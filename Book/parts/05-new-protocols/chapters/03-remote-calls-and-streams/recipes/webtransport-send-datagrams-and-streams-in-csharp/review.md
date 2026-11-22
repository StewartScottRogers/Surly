<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: webtransport-send-datagrams-and-streams-in-csharp
Verdict: accept with findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. Nearest honest recipe. Program finding: Curl.Api 0.2.0 opens a WebTransport session (extended CONNECT answered 200, login judged) but sends no data on it: a Body (bytes or stream) on POST to webtransport:// with or without ?wt=datagram|uni is never sent (surl's record shows only the CONNECT), StatusCode 0, empty body. Expected: the Body sent as curl -d sends it, and the echo read back. .NET has no WebTransport client and System.Net.Quic has no datagrams, so the twin checks the login gate and surl's record (Decoded header blocks, LoginJudged events) and claims the limitation (no-data-yet). The title promises datagrams the C# surface cannot send yet; the lead may want to retitle or hold it.
2. LoginJudged for a refused session can arrive after SendAsync throws; the program waits for it with WaitForEventAsync.
- Lead: retitled "Opening a WebTransport Session in C#", since Curl.Api 0.2.0 sends no body over webtransport:// (reported to Curl); the datagram claims stay with the console and agent twins.
