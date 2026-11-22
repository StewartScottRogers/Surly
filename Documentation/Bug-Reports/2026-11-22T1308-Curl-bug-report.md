<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (batch 12 of the 1,000-recipe plan)

From writing and verifying batch 12 (62 recipes) of the *Surly* cookbook against Curl `c84035f797`, Surl `5da5b3eaf`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | curl-mcp | The `curl` tool with `-o` into a folder that does not exist returns plain text `An error occurred invoking 'curl'.`, not the JSON result with curl's exit 2, as the console gives |
| 2 | Medium | Curl.Api SignalR | `Curl.Api` 0.2.0 cannot make a SignalR hub call: only the negotiate request is sent, as https or http |
| 3 | Medium | Curl.Api Socket.IO | With a Socket.IO session open, `SendAsync` blocks until the timeout (28); the echoed event is never returned |

## Repros

**1.** `surl --mail-domain example.com --mailbox bob@example.com:bobpass smtp://127.0.0.1:2525/ pop3://127.0.0.1:1110/`, three messages delivered to bob.
- Console: `curl -sS -u bob@example.com:bobpass -o "nosuch/msg#1.eml" "pop3://127.0.0.1:1110/[1-3]"`, where `nosuch` does not exist → exit 2.
- curl-mcp `curl` with the same arguments.
- Observed: a tool error with the plain text `An error occurred invoking 'curl'.`
- Expected: the JSON result with exit 2 and curl's message.
- Recipe `pop3-drain-a-maildrop-from-an-agent`.

**2.** `surl --cert server.pem --key server.key --user alice:s3cret signalrs://127.0.0.1:5001/`.
- Curl.Api `POST` `signalrs://127.0.0.1:5001/` with a hub invocation body.
- Observed: the negotiate response only; no invocation reaches surl.
- Expected: the hub call sent, as `curl -d` does.
- Recipe `signalrs-log-in-to-a-hub-in-csharp`.

**3.** `surl --cert server.pem --key server.key --user alice:s3cret socketios://127.0.0.1:5002/`.
- Curl.Api `SendAsync` with a Socket.IO event and `Timeout = 5 s`.
- Observed: `CurlException` 28, although surl logs the join and the event.
- Expected: the echoed event returned.
- Recipe `socketio-log-in-to-a-namespace-in-csharp`.
