<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-list-a-folder-over-websocket-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. ConnectWebSocketAsync throws CurlException HttpReturnedError (22) for every refused upgrade; the exception alone does not say why.
2. A file is a Binary message and a listing a Text message; a console curl cannot show that.
