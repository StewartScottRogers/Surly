<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socketio-receive-an-event

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 8 of 8 cases pass on windows.

## Findings

1. Deterministic end: a Socket.IO session has no last message and curl ends only when the server disconnects, which surl does not do to a live client. Every case that joins is bounded by `-m` (2 to 10 s, case timeouts 30 s), so the solution exits 28 with the event already written. The Discussion says plainly that 28 is the expected end. No case is a race.
2. Fixed in drafting (Truth 5): "broadcast reaches every session" removed; with one curl run only the sender's single copy is proven (`broadcast-once`), for the same reason as in signalr-receive-a-message.
3. `--allow-anonymous` has its refusal (`refused-by-default`) and effect. `no-login-from-curl` shows that the Curl port's client sends no login in the namespace request (-u is ignored), so `--allow-anonymous` is the only way it can join surl today. `--allow-plaintext-auth` is on that server to rule out the plain-text refusal as the cause; its own effect cannot be shown with this client. Reported to the lead.
4. Source wanted, not cited: the Engine.IO protocol, version 4 (the `/socket.io/` path and heartbeats).
