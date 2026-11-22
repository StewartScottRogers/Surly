<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-keep-readings-across-restarts-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows (agent).

## Findings

1. Carries the console claims; restart is stop_server and a second start_server on the same absolute --directory under SURL_MCP_ROOT. Adds a seeded reading (publish_message retain true) that survives.
2. Publishes wait with wait_for_event exchange_ended, bounded at 10 s. A folder in use or missing is a start_server tool error with exit_code 124 or 37.
