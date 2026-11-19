<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-wait-for-an-event-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.
## Findings
1. Title improved to "Waiting for an Upload from an Agent"; key unchanged.
2. verify runs calls in order, so the solution starts the client before `wait_for_event`; the Discussion says a matching event already kept is answered too, which the solution proves. A wait that is satisfied while blocked cannot be a case.
3. Fixed: "so an agent cannot wait forever by accident" and a Digest-never-sends-the-password aside had no case; removed. The 403 for Basic is now a case, and the why is left to <<http-refuse-a-password-in-clear>> (not written yet).
4. Schema limitation: a saved value is substituted as a string, so `after_sequence` (an integer) cannot take a saved `sequence`; surl-mcp answers a string there with a JSON-RPC Internal error. Paging is shown with `max_events` only.
5. Observed, not claimed: sequence numbers have gaps (1, 5, 7, 8) and `kinds: [exchange_bytes]` answers nothing; the Discussion says only "numbered", never "consecutive".
6. An empty `events` list cannot be proven by result-contains, so non-matching filters are proven with `wait_for_event` timing out.
