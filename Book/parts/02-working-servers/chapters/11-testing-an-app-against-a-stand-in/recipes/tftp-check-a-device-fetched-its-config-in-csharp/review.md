<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-check-a-device-fetched-its-config-in-csharp
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.

## Findings

1. The check polls FindEvents(ExchangeEnded)/FindExchanges every 200 ms for at most 10 s and decodes the recorded datagrams.
2. Reports data blocks where the console reports surl's 'in blocks of' line: the twin's own words for the same read.
