<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: signalr-stream-items-from-a-hub-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.

## Findings
1. Program finding: Curl.Api 0.2.0 cannot make a stream invocation. SendAsync to signalr:// with a Body (POST, PUT, FromStream) negotiates and handshakes, never sends the call, and times out with 0 bytes (curl -d works on the console). The recipe therefore hosts the hub with Surl.Api and speaks the hub protocol by hand with HttpClient and ClientWebSocket; it does not use Curl.Api. Nearest honest recipe, not the planned one.
2. The Discussion makes no claim about Curl.Api; the finding is here and in facts/signalr.md.
