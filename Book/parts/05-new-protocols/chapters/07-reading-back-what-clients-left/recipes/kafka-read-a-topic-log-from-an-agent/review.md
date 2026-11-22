<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-read-a-topic-log-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 7 of 7 cases pass on windows (agent).

## Findings

1. Fitness (4): overlaps kafka-read-a-topic-log-after-a-test (C#), by design; this one is the agent's read_log by offset, with curl-mcp as the producer.
2. Truth (8): whole log, from an offset, paging, replay by a curl consumer after the read, past the end, no such topic and a refused producer (exit 56) are cases. No record keys: nothing here can set one.
