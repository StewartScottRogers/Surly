<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (re-pin to Curl 2145a49eae and Surl v0.1.5)

From re-verifying the *Surly* cookbook's 833 recipes on Curl `2145a49eae` and Surl `d64faa4bb` (v0.1.5), Windows 11. BL-5758 fixed the C# body gaps for MCP, WebTransport and SignalR: those twins now carry their console siblings' claims. One new finding.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Socket.IO login | No curl option sends a Socket.IO token. Surl (BL-1589) takes a token account only as `{"token":"..."}` in the namespace CONNECT's auth payload; `-u` always sends `{"username":"...","password":"..."}`, and `--oauth2-bearer` is not used there. So a token login can't be shown on any surface (console, Curl.Api, curl-mcp) |

## Repros

**1.** `surl --cert server.pem --key server.key --user :tok123 socketios://127.0.0.1:5002/`.
- `curl -k -u :tok123 -d '["hello","hi"]' socketios://127.0.0.1:5002/` → refused, `Not authorized`.
- `curl -k -u tok123: ...` → refused.
- `curl -k --oauth2-bearer tok123 ...` → refused; surl's log shows the auth payload as `{"username":...,"password":...}` or none.
- Expected: some way to send `{"token":"tok123"}`. Perhaps `--oauth2-bearer` maps to it for socketio(s), as Socket.IO clients' `auth: { token }` does.
- Recipes `socketio-log-in-to-a-namespace` and twins.
