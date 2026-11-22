<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-subscribe-over-websocket

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf) and Curl commit c84035f797: 12 of 12 cases pass on windows.

## Findings

1. curl publishes and subscribes over mqtt5+ws:// (path /mqtt added by curl); a plain ws:// client is 400 (no subprotocol) or 404 (other path), curl exit 22.
2. No encrypted WebSocket listen URL for MQTT in this build (surl --version lists mqtt5+ws only); the Discussion says so.
