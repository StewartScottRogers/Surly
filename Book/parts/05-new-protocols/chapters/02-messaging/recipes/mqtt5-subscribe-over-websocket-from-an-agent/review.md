<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-subscribe-over-websocket-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl-mcp 1.0.0 (Surl commit 5da5b3eaf) and curl-mcp (Curl commit c84035f797): 12 of 12 cases pass on windows.

## Findings

1. Carries all twelve console claims; the curl tool publishes over mqtt5+ws:// directly.
2. Publishes wait with wait_for_event exchange_ended, bounded at 10 s.
