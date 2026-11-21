<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-offer-a-single-cipher-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Truth: as in the C# twin, the offer is read from `get_exchanges` (the server's key exchange message, plaintext chunks), since neither host can play a client that restricts its algorithms.
2. Fixed: a refused cipher or MAC is a `start_server` tool error carrying surl's own line.
