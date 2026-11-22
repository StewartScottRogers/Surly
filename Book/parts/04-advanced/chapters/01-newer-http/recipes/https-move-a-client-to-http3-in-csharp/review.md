<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-move-a-client-to-http3-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 windows cases pass; the [linux, macos] case is skipped here.
## Findings
1. Same platform split as the console twin; Curl.Api follows ADR-1313 on Windows.
2. Fixed: without a QUIC warm-up the `Http3` request lost the race to TCP in 2 of 3 runs and once hung past 60 s; a warm-up `Http3Only` request on a separate client fixed it (3 of 3 by hand, then verify).
3. QUIC is shown from the server's record: `SurlExchangeChunk.StreamId` is set only on QUIC exchanges.
4. Revised after the Curl session (ADR-1313 stands; with --http3 a stored h3 entry is used on Windows too): the solution passes --http3 (Http3) and runs on all three platforms; the no-version-option switch is a [linux, macos] case and the stay on HTTP/2 a [windows] case.
5. Bounded wait before reading the server's record (CI race sweep).
