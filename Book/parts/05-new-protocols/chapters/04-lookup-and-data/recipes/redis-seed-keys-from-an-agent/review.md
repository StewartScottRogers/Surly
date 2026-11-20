<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-seed-keys-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 9 of 9 cases pass on windows.
## Findings
1. Written as a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19), one program, `keys.cs`, its scenario chosen by argument; each scenario starts its own server.
2. Fitness (3): the server takes `--user alice:s3cret --allow-plaintext-auth` so that `curl` can log in over `redis://` and the recipe can show `set_key` needs no login while a client does. The loosening is named in the Discussion, with a pointer to the console recipe for the refusal and `rediss://`.
3. Timing: `expires` sets `expire_ms` 1000 and waits 3 seconds, with a 90-second case timeout; deterministic, not a race. `expires_at` (a timestamp) is not printed.
4. Observed, not claimed: `set_key` answers `key` with the database in front (`0/greeting`) while `read_key` and `list_keys` answer the bare key; `set_key` also succeeds on a server with no Redis listener (an HTTP-only one), and `set_key` with neither `value` nor `value_base64` is a tool error, `give exactly one of value and value_base64`. Left for the author.
5. Fixed: removed an unchecked sentence that the read tools never change the store; cited the README for the keys being memory-only.
6. Updated 2026-11-19 for Surl 763754785 and Curl b9ca725077: `set_key` now answers the bare `key` with `database` beside it (and `event_name` `0/greeting`), so keys.cs prints `greeting in database 0`; `set_key` on a server with no Redis listener is now a tool error, `This instance holds no Redis keyspace.`, shown by new case `no-redis-listener` (an HTTP-only server). Finding 4's `0/` and no-listener observations no longer hold.
