<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-keep-a-queue-across-restarts
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Truth: every sentence of the Discussion has a case; restart: stops surl outright and starts the same command on the same ports.
2. surl accepts ;durable with no --directory and keeps nothing; the recipe says so and the memory server shows it.
3. Removed from the draft: a claim that curl-declared queues are kept (probed once, not cased).
