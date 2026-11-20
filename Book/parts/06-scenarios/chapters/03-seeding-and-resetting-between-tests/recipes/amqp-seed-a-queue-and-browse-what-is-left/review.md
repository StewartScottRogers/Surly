<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-seed-a-queue-and-browse-what-is-left
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Seeding with Messages.EnqueueAsync, the worker as Curl.Api AMQP gets, the check with BrowseQueueAsync, and ResetAsync between tests.
2. Curl.Api, like curl, logs in to AMQP with PLAIN only, so the server needs AllowPlaintextAuthentication (refusal cased, CurlException 67).
3. In flight is 0 here because a curl get acknowledges at once; the agent recipe shows a nonzero count.
