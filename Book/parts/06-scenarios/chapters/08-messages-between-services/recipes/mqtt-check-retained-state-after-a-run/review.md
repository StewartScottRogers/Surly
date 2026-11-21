<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-check-retained-state-after-a-run

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. read_retained_message answers {retained: false} for a topic with nothing kept: not a tool error. surl-mcp has no call that lists topics, so the agent must know them.
2. A publish outside the --topic filter is accepted (curl exit 0) and not kept.
3. --allow-anonymous has its refusal case (curl exit 8) in login-required.
4. Chapter intro 06/08 written.
5. CI run 44835381862: on macOS the last device's QoS 0 publish was not yet kept when read; the program now waits for each publish's `exchange_ended` event (bounded 10 s), no sleep.
