<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-log-in-over-tls-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 cases pass on windows.
## Findings
1. Same claims as the console recipe. A Curl.Api POST does not produce a Kafka record, so the record is the server's own `PublishAsync`.
2. The TLS handshake failure message is the platform's, so only the code is printed for codes other than 67.
