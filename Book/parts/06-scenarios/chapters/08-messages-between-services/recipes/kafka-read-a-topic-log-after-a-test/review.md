<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-read-a-topic-log-after-a-test
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. Planned task said key and value; no producer here can set a key (curl has no option, ?key= is refused; PublishAsync takes none), so the recipe checks offsets and values and shows every key empty. Program finding for the author: no way to produce a keyed record.
2. Curl.Api cannot produce to Kafka (BL-5611); the service stand-in writes with PublishAsync.
