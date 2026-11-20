<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-check-a-retained-message-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): None of None cases pass on windows (console).

## Findings

1. Fitness (3): the QoS read back is always 0 here, because curl and publish_message publish at QoS 0; `;qos=n` is a cap (cited, with a case that qos=1 does not raise it). No way to produce QoS 1 or 2 with these builds, so the recipe shows the QoS field and what it is, and says so.
2. Truth (7): the retained false result, the empty message clearing, the base64 answer for a non-text payload, the filter and the seeded exception, and the 0x87 refusal are cases.
3. A curl subscribe needs --mqtt5-disconnect-after-retained on the broker or it never ends; it is in the arguments and explained.
