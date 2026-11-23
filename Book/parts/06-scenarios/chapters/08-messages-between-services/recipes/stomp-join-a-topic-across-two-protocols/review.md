<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: stomp-join-a-topic-across-two-protocols
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, no packages: 6 of 6 cases pass on windows.

## Findings

1. The new service is a live MQTT 5 subscriber; join.sh waits for it with a ping sent over STOMP (ten tries), then waits for the last order (ten tries).
2. Retained messages are MQTT's alone (stomp.md): cased as the STOMP subscriber getting nothing while the MQTT 3.1.1 read gets order 1002.
3. Failure paths (dotted name, queue) each take about ten seconds; timeout 90.
