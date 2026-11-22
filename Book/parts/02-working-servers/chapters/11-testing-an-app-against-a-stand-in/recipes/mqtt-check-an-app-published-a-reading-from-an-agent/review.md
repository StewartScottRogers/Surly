<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-check-an-app-published-a-reading-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows (agent).

## Findings

1. Carries all eleven console claims with the curl tool as the app, wait_for_event exchange_ended after each publish (bounded 10 s) and read_retained_message as the check; retained false is not a tool error.
2. The refused case ends at the connection, so it waits for nothing.
