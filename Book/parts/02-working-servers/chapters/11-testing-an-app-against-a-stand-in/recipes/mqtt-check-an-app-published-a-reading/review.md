<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-check-an-app-published-a-reading

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows (console).

## Findings

1. check.sh polls the kept reading every half second, 20 tries by default, until it equals the expected one; the failing cases pass 2 so they end quickly.
2. A misspelt topic, a topic outside the filter and a server with no --topic all leave nothing kept; the check says so by name.
