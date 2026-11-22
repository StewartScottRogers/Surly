<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-stand-in-for-a-service-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. An agent cannot hold a session between tool calls: the listening side is a concurrent curl-tool subscriber (?count=1), the answer is publish_message to the reply subject written as a topic (_INBOX/<hex>), found by polling get_exchanges (bounded).
2. The server record shows the app's PUB (received) and the MSG it sent the app (sent).
