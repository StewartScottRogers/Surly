<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqps-log-in-over-tls-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 9 of 9 cases pass on windows.

## Findings
1. Curl.Api cannot publish over AMQP, so the message is put on the queue with Messages.EnqueueAsync, as the other AMQP C# recipes do.
2. The 60 message is the TLS library's, so only the code is printed; only Windows run.
