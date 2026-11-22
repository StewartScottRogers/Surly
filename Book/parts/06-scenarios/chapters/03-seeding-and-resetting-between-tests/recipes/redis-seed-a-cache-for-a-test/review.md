<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-seed-a-cache-for-a-test

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows (console).

## Findings

1. Fitness (3): one surl serves Redis and HTTP with one account; --user covers both listeners (HTTP without a login is 401).
2. Timing: the expiring key is set and read in one curl run (PX 3000, sleep 4); the C# and agent twins make the first Curl.Api / curl-mcp request before any short expiry is set, then use 4 s and wait 5 s.
3. Truth (7): plaintext refusal is a case on its own server (exit 67 WRONGPASS).
4. The twins carry every console claim: seed, read back, miss after expiry, origin over HTTP, TTL, forced miss, origin statuses, plaintext refusal.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 404 now carries the body `404 Not Found` (BL-1444); origin-content gives each URL its own -o so the body stays off standard output.
