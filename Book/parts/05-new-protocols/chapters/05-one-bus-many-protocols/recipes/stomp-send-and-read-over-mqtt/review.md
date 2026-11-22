<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: stomp-send-and-read-over-mqtt

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 8 of 8 cases pass on windows.

## Findings
1. A STOMP subscriber is not sent the retained message an MQTT subscriber gets (case stomp-subscriber-gets-nothing); reported as observed, not as a defect.
2. One probe hung a STOMP queue subscribe without -m past 120 s; not reproduced; every subscribe here has -m or the idle timeout.
3. The retained-message claims rely on --topic ...;retain, as the MQTT recipes do.
