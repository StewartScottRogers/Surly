<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-browse-a-queue-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 6 of 6 cases pass on windows (agent).

## Findings

1. Fitness (4): distinct from amqp-check-what-a-queue-still-holds (a worker and in-flight): this one is order, paging, and that a browse takes nothing (a later curl get gets the first job).
2. Truth (8): every publish is followed by a bounded wait_for_event for the enqueue; empty queue, unknown queue and refused login are cases.
