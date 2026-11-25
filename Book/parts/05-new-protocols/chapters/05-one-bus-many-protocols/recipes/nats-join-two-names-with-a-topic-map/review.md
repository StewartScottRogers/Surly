<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-join-two-names-with-a-topic-map
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The MQTT-to-NATS case uses a second server whose MQTT connections are held 1 s by --fault (scheme=mqtt, accept stage), so the -Z NATS subscriber is in before the publish.
3. Not claimed (reported): with the map, an MQTT publish to orders/created (the mapped name's old default) reaches no NATS subscriber, not even '>'; and a NATS wildcard subscription (orders.>) does not match the mapped name orders.created.
