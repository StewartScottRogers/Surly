<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: stomp-send-and-read-over-mqtt-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 8 of 8 cases pass on windows.

## Findings
1. Waits are bounded loops on ExchangeStarted/ExchangeEnded counts; Curl.Api POST publishes over stomp://, unlike amqp://.
