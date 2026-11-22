<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-check-what-a-queue-holds-in-process

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 5 of 5 cases pass on windows (C#).

## Findings

1. Fitness (4): Curl.Api cannot publish over amqp://, so the app queues over STOMP, which reaches the same queue (facts/amqp.md); the recipe says so.
2. Truth (8): order, ready and in-flight counts, paging, a take by a worker, and two failures are cases. Finding: a STOMP SEND to an undeclared queue creates it silently (case wrong-queue), unlike EnqueueAsync, which is refused.
3. In-flight messages are not shown here (see amqp-check-what-a-queue-still-holds).
