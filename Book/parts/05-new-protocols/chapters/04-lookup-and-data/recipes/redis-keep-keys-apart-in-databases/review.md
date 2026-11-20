<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-keep-keys-apart-in-databases

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows (console).

## Findings

1. Fitness (3): the database number is the first URL path segment; the Solution shows two databases and the default, the Discussion the scoping of DBSIZE, DEL, FLUSHDB.
2. Truth (7): the loosening --allow-plaintext-auth has its refusal (WRONGPASS, exit 67) on the strict server and NOAUTH without a login; both are cases.
3. Program finding: none. Database 16 is exit 8 `ERR DB index is out of range` from curl; set_key bounds database at 15 in its schema.
4. Cited, not cased: Redis has sixteen keyspaces (<<redis-resp>>); rediss:// is the secure way.
