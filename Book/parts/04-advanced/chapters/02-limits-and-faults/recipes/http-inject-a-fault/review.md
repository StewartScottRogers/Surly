<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-inject-a-fault

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 15 of 15 cases pass on windows (three runs).

## Findings

1. Three servers keep the claims independent: `main` (503 once, then served), `flaky` (delay, drop and a 503 for `--retry`, path rules first, since the first matching rule wins) and `accounts` (refuse-login, out-of-space). `times=1` rules are at the command stage, so verify's readiness connection does not spend them; an accept-stage `times` rule would be spent by it (probed), which is why none is used.
2. `once-only` depends on running after `solution`; cases run in order.
3. `delay` claims the wait with `stdout-matches '^200 [2-9]\.'` on `%{time_total}`, a lower bound only.
4. `drop` exits 56 (connection reset): `surl` aborts the connection. Upstream curl reports a reset the same way on Linux and macOS, but only Windows was run.
5. Fixed in drafting: removed "surl warns on start" (the Surl README does not say it for `--fault`, and verify cannot see the log) and "loosens nothing" (`surl --aihelp testing` marks `--fault` as loosening, for tests only). Out-of-space is described by its answer, 413, not as "what a full store would say", which only Surl's ADR-0129 states.
6. Program note: `out-of-space` on HTTP is `413 Content Too Large` by design (ADR-0129), not `507 Insufficient Storage` (RFC 4918, not in the bibliography).
Re-pinned to Surl a423807a5d / Curl 0680b97577: the HTTP error fault now answers with a short body (BL-1444), which curl --retry writes before the file in retry-recovers; --fault gained the body key, added to keys-and-limit.
