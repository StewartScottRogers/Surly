<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-bridge-to-amqp-1-0-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows.
## Findings
1. Same ten claims as the console recipe; sends go over both versions with the curl tool, and each send waits for the bus event enqueued (no fixed sleep).
2. An AMQP 1.0 receive from an empty address is a tool error, exit 28 after -m 2.
3. Bounded wait before reading the server's record (CI race sweep).
