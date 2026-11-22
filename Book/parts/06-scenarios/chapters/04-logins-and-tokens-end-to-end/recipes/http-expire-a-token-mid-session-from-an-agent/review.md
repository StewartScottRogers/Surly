<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-expire-a-token-mid-session-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 8 of 8 cases pass on windows.

## Findings

1. Same registry design; reset_server revokes. add_account with user "" fails schema validation (McpProtocolException), not an isError result.
2. Records: wait_for_event per login with after_sequence, then get_events.
