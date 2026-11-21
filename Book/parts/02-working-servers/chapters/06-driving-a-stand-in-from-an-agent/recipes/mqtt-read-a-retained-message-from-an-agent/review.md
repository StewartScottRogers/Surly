<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-read-a-retained-message-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. No program finding. read_retained_message answers retained false, not an error, for an empty topic; retain false keeps nothing; a curl publish is followed by wait_for_event before the store is read.
2. Overlaps mqtt5-check-a-retained-message-from-an-agent; this one is MQTT 3.1.1, adds binary, large and wildcard cases.
