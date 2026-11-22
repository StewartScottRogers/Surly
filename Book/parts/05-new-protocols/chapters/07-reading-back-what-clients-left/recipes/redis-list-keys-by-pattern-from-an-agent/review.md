<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-list-keys-by-pattern-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 6 of 6 cases pass on windows (agent).

## Findings

1. Fitness (4): the cache is curl-mcp writing seven keys in one run; list_keys and read_key are the agent's side; the last write is waited for (keyspace event, bounded).
2. Truth (8): pattern, paging with after_key, ? and a range, no match, expires_at and no such key are cases; the expiry time is not printed (it changes).
