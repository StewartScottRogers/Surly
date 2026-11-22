<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-check-what-an-app-cached

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 6 of 6 cases pass on windows (C#).

## Findings

1. Fitness (4): the app is a stand-in class that caches over Curl.Api (Redis commands in the URL path); no Redis client library is pinned.
2. Truth (8): origin requests are counted from the server's own record of its HTTP connections (Received/Sent bytes of FindExchanges), not from ExchangeEnded events, which a kept-alive connection never raises (facts/redis.md); the read waits at most 10 s.
3. Cases show the count is a count: a second product, a deleted key, a 404 and an app that never writes its cache (exit 1).
