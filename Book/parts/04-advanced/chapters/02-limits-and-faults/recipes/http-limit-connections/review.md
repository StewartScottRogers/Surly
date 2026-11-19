<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-limit-connections

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 8 of 8 cases pass on windows (three runs, one with five recipes side by side).

## Findings

1. Concurrency without a race: every server holds `/slow.txt` two seconds with `--fault "delay;stage=command;..."`, and `curl -Z --parallel-immediate` opens both connections at once. Both URLs are the same path, so whichever connection wins, the refusal prints first and the 200 two seconds later: the output `503\n200\n` does not depend on accept order. Without `--parallel-immediate` curl waits for the first transfer before opening the second, and nothing is refused.
2. Per-address needs two client addresses: `http://localhost:8082/` binds 127.0.0.1 and ::1, and the claim uses one of each. On a machine with IPv6 loopback disabled, `other-address` would fail; macOS has no 127.0.0.2 by default, which is why IPv6 is used instead.
3. `--fault` is in the recipe only to hold a connection; the Discussion says so and points to <<http-inject-a-fault>>.
4. Defaults (1024, 100, 0 for none) are a case on `surl --aihelp limits`, not a citation: the Surl README does not state them.
5. The refusal's header block is matched whole (Server, Content-Length 0, Connection close), so "empty body" is proven by Content-Length 0.
