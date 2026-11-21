<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-move-a-device-to-mqtt5-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 12 cases pass on windows.
## Findings
1. Same claims as the console recipe, with the MQTT 5 side of a message seeded (see the console review, finding 1).
2. A refusal returns the CurlException code (8 or 67) as the program's exit code.
