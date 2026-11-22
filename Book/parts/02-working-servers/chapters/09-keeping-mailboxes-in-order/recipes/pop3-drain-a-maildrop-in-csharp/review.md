<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-drain-a-maildrop-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows (C#).

## Findings

1. Carries all ten console claims; each case lists, retrieves into files, then DELEs with one CurlClient, disposes it (QUIT) and waits for ExchangeEnded (bounded 10 s) before reading the store.
2. The failed save is File.WriteAllBytesAsync throwing before the DELE loop.
