<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-refuse-a-publish-by-reason-code

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf) and Curl commit c84035f797: 9 of 9 cases pass on windows.

## Findings

1. The title promises a refused publish with a reason code; curl publishes at QoS 0, which gets no PUBACK, so a refused publish is dropped silently (exit 0) and the recipe says so and proves it with a later read. Reason codes shown: SUBACK 0x87, CONNACK 0x86 and 0x87.
2. Plain-password refusal is 0x87 over mqtt5 (0x86 only when the password is checked and wrong).
