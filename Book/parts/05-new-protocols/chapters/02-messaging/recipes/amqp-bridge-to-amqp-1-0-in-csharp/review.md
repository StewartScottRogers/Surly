<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-bridge-to-amqp-1-0-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows.
## Findings
1. Program finding for the author: Curl.Api 0.2.0 cannot publish over AMQP. A CurlRequest with Body (POST or PUT) to amqp:// or amqp1:// does a receive (basic.get; on AMQP 1.0 it waits), status 0, nothing queued, like Kafka (BL-5611). The program puts the message on the queue with EnqueueAsync and says so; the console twin sends with curl -d, so the cross-version send is shown there and in the agent twin, not here.
2. An empty AMQP 1.0 receive needs a CancellationToken; it ends with OperationCanceledException.
