<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-find-why-a-subscriber-missed-a-message
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 6 of 6 cases pass on windows.
## Findings
1. Bus store_changed events tell the three publish outcomes apart: written + published (kept), published only (outside the retain filter), none (dropped for want of a grant). Not documented in the fact sheet before; added to mqtt.md.
2. get_exchanges MQTT chunks split packets and mix text and base64; the program joins each direction's bytes and cuts packets by the fixed header.
3. Waits use exchange_ended after every curl call (platforms.md, unacknowledged QoS 0). `--allow-plaintext-auth` has its refusal case (CONNACK 5).
