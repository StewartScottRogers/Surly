<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-test-a-shared-team-folder-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. As the console twin: the message reaches the folder by APPEND through the curl tool; seed_mail to team@ is a tool error (NoSuchAccount), case seed.
2. list_messages takes the member as owner and "Shared/team" as mailbox; the reserved owner ":shared" is not reachable from the tools.
3. Self-review against REVIEW.md; no independent reviewer yet.
