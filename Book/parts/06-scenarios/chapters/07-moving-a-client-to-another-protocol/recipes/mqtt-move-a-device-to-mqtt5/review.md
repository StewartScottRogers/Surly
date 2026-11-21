<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-move-a-device-to-mqtt5
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 12 cases pass on windows.
## Findings
1. Curl.Api 0.2.0 does not publish over mqtt5://: a POST sends a SUBSCRIBE and nothing is kept (status 0, no error). The C# twin plays the MQTT 5 side of a message with `Messages.PublishAsync`, and its claims say so; the console and agent twins publish over MQTT 5 with curl.
2. A refusal differs by version: MQTT 3.1.1 is exit 8 `Expected 0000 but got 0004|0005` (Curl.Api `WeirdServerReply`); MQTT 5 is exit 67 `CONNACK reason code 0x86 BadUserNameOrPassword|0x87 NotAuthorized` (`LoginDenied`).
3. Retained publishes and reads wait on the server's `exchange_ended` event, not a clock.
