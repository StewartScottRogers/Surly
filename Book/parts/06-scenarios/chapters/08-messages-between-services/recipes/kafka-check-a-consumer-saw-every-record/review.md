<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-check-a-consumer-saw-every-record
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. The consumer is the curl tool; what it reports is its output. A consumer that misses records is a stale offset (skipped) or one that ended early (late); no real consumer is run.
2. Kafka produces are acknowledged before they return, so no wait is used.
