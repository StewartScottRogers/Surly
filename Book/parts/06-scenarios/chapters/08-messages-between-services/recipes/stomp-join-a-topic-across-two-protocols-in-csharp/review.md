<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: stomp-join-a-topic-across-two-protocols-in-csharp
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. Curl.Api GET over mqtt5:// streams each message on the response body; the same CurlClient sends the STOMP POSTs meanwhile.
2. The broker's record is read with Messages.ReadRetainedAsync, polled for at most 10 s.
