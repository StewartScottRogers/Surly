<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: signalr-receive-a-message

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 7 of 7 cases pass on windows.

## Findings

1. Deterministic end (the brief's rule): the solution invokes `Broadcast` with an invocation id, so the hub's call back to the caller arrives before the call's completion, and the completion ends the run with exit 0. No case waits on a race.
2. Fixed in drafting (Truth 5): "Broadcast reaches every connection" removed. Two curl runs, one listening and one broadcasting, cannot be ordered without a race (the listener may not have finished its handshake), so only the caller's own copy is claimed. A program on `Curl.Api` could show the second connection deterministically; that is a C# recipe, not this one.
3. `listen-hub-timeout` takes about 30 seconds: curl sends no keep-alive Ping, so the hub's 30-second client timeout closes a listener with nothing to send. The claim checks the elapsed time with `-w "%{time_total}"` (30 to 39 s) and the hub's error text. A listener cannot stay longer than that; noted for the lead (curl's conformance matrix marks keep-alive n/a).
4. `--allow-anonymous` has its refusal (`refused-by-default`, negotiate 401) and its effect (every other case). Logging in with an account over `signalrs` is named, not shown.
5. Source wanted, not cited: SignalR TransportProtocols.md (negotiate and the WebSocket transport); the Discussion's negotiate sentence rests on it and on the 401 case.
