<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-a-test-proxy-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.

## Findings

1. `transfer` has a `proxy` field but no tunnel field; the tunnel case uses the `curl` tool with `-p`.
2. `start_server` with an empty `listen_urls` is a JSON-RPC error, so the proxy rides beside an origin listen URL.
