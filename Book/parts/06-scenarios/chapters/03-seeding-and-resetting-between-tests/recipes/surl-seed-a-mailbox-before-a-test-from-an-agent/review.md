<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-seed-a-mailbox-before-a-test-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. The client under test is curl-mcp's curl tool; -X "DELE 1" deletes on QUIT at the end of the command line.
2. Failure path: seed_mail to an address with no mailbox is a tool error NoSuchAccount.
3. Bounded wait before reading the server's record (CI race sweep).
