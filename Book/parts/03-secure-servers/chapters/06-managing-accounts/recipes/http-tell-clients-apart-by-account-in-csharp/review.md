<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-clients-apart-by-account-in-csharp

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.

## Findings

1. RecordExchanges is off by default; the recipe shows the InvalidOperationException rather than hiding it.
2. An exchange is a connection: the one-client case shows accounts mixing in one exchange; the wrong guess uses its own client to keep the solution clean.
