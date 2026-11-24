<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-spot-a-truncated-recording
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. One CurlClient per fetch so each fetch is one exchange; with MaxRecordedExchanges 5 and MaxRecordedBytesPerExchange 4096, 4 dropped and exchange 9 (13000-byte export) truncated.
3. The event count nine fetches make is not claimed; only that DroppedEventCount is above 0 with MaxRecordedEvents 10.
