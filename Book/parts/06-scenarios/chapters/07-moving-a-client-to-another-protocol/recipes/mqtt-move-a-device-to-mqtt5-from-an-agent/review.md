<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-move-a-device-to-mqtt5-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 12 cases pass on windows.
## Findings
1. Same claims as the console recipe; every device is a curl-mcp call, an MQTT 3.1.1 subscriber's body is decoded from its length, topic and payload.
2. After each refusal `read_retained_message` answers `{"retained":false}`.
