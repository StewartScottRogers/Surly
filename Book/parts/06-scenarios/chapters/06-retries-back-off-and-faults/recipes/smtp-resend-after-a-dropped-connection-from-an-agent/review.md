<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-resend-after-a-dropped-connection-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Same 11 claims as the console recipe; the log claims are shown through get_events and get_exchanges.
2. The drop rule is added with add_fault after start, so nothing spends it first. Delivery is waited for with wait_for_event, after_sequence and a ten-second bound.
3. exit 56 on a dropped connection is seen on Windows; Linux and macOS not yet seen.
