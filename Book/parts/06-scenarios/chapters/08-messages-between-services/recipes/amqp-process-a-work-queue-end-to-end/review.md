<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-process-a-work-queue-end-to-end
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows.
## Findings
1. The worker is a STOMP client because curl amqp:// takes and acknowledges at once, and its frames are fixture files with NUL bytes, so no shell is needed.
2. A job the worker did not acknowledge before its DISCONNECT is ready again: shown with two.stomp and a later receive.
3. Not shown on the console: the server event record (enqueued, delivered, acknowledged); the C# and agent twins show it.
