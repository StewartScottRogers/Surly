<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-fall-back-when-http3-is-missing-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. The first verify run timed out (solution, 60 s) and the h3 contrast came back HTTP/2 under heavy load; the first QUIC handshake in a process is slow. Fixed with a warm-up `Http3Only` request on a separate client in the h3 scenario; passes since. Worth watching in CI.
2. Fixed: pairing TCP and UDP on one port from port 0 failed once with `(45) Could not bind ...: Permission denied` (Windows reserved ranges); the program starts HTTPS first, then TFTP on its port, and retries up to five times.
3. `ConnectTimeout` on the request's `CurlNetworkOptions` bounds the swallowed `Http3Only` case (28); without it libcurl waits 300 s (the console twin's finding). The exception message is platform wording, so only `Code` is printed.
Re-pinned to Surl 5da5b3eaf / Curl c84035f797: swallowed-only crashed with SurlServerStartException `(45) Could not bind tftp://...: Permission denied` after five tries: Windows reserved UDP blocks of 100 ports, and sequential TCP ports stayed inside one. Both servers now take a random port number in 20000-40000, ten tries; 16 of 16 swallowed runs passed.
