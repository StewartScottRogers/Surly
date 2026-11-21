<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-process-a-work-queue-end-to-end-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 13 of 13 cases pass on windows.
## Findings
1. Thirteen cases against ten on the console: three more show the get_events record. The record is polled, with a bound, until the last acknowledgement is in; each send waits for the enqueued event.
