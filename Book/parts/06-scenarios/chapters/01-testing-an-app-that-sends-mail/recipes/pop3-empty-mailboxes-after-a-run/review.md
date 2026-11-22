<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-empty-mailboxes-after-a-run
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Probed, not claimed: in a --next chain of DELEs, one that fails (-ERR, exit 8) does not undo the DELEs before it; they are committed.
2. With a wrong password curl prints Login denied once per --next transfer; the case checks both lines.
3. That DELE only marks and QUIT commits is cited to <<rfc1939>>; the cases show the counts after the call.
4. Bounded wait before reading the server's record (CI race sweep).
