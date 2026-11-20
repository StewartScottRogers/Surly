<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-hand-bound-ports-to-the-app-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0 (local feed): 3 of 3 cases pass on windows.
## Findings
1. In-memory Files instead of --directory, so two servers side by side do not clash; the console twin carries the directory clash as its case.
2. SMTP through Curl.Api needs a stream body (CurlContent.FromStream) with PUT; a byte[] body with GET is answered 500 (probed, not claimed).
3. Failure path: a second server asking for the first one's bound port, SurlServerStartException BindFailed (45).
