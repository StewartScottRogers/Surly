<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-check-what-a-queue-still-holds
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows (in-flight passed in 4 runs of 4).
## Findings
1. Nearest honest recipe: curl's AMQP get is basic.get with no-ack, so no curl AMQP worker can hold a job in flight. The in-flight scenario's worker uses the same surl's STOMP listener (same bus queue) through the curl tool's telnet:// with a frames fixture and -m 3; protocols lists stomp beside amqp. Said in the Discussion.
2. Timing: the agent waits for two acknowledged events with wait_for_event rather than sleeping; the requeue after the worker's connection closes was in place before the next browse in every run.
3. browse_queue lists ready messages only; in-flight ones are a count, so the report names ready jobs and counts unfinished ones.
