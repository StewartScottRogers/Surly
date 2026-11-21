<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-keep-a-queue-across-restarts-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Same seven claims as the console recipe; the jobs are queued with the curl tool and each send waits for the bus event enqueued before the next.
