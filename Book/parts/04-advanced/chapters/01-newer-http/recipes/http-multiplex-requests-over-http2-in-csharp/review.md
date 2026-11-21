<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-multiplex-requests-over-http2-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. `PipeWait = true` puts five concurrent `SendAsync` calls on one HTTP/2 connection (1 exchange); without it 5; HTTP/1.1 with it 5. Cased all three.
2. A warm-up request on a separate client pays Curl.Api's one-time set-up before anything is counted or timed; connections are the growth of `server.Exchanges`.
3. Fixed: cut a comparison with `curl -Z --parallel-immediate`, which the console twin found nondeterministic.
4. CI fix (run 44871788391): without `PipeWait` Ubuntu saw 2 connections, Windows 5: a race. `without-pipewait` now claims more than one (`connections: [2-5]`).
- CI (batch 7, macOS): without PipeWait, one connection was seen; the claim is now one to five, against PipeWait's always one.
5. CI fix (run 44878668055): macOS reused a finished HTTP/1.1 connection (4, not 5). Every request is now held 1 s (`SurlFault.Delay`, stage command), so the five overlap: PipeWait 1 and HTTP/1.1 5 are exact again; without-pipewait stays a range `[1-5]`. Verified three times on Windows.
