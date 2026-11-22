<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-process-a-work-queue-end-to-end-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 13 of 13 cases pass on windows.
## Findings
1. Thirteen cases against ten on the console: three more show the event record (FindEvents with RecordExchanges). Curl.Api cannot publish over AMQP, so the jobs come from EnqueueAsync; the STOMP worker runs through Curl.Api as the body of a telnet:// request.
2. A restarted server has a new event record: no enqueued, two delivered, two acknowledged.
3. Bounded wait before reading the server's record (CI race sweep).
