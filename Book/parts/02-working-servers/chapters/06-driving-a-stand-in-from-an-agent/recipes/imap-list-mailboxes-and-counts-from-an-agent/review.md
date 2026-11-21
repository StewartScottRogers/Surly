<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-list-mailboxes-and-counts-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. list_messages entries are {uid, size, flags[], internal_date}; the flag string is `\Seen` (one backslash). Recorded in facts/mail.md.
2. The seeds are waited for with wait_for_event and after_sequence; the curl tool's APPEND to Sent is synchronous (curl exits 0 after the server's reply).
3. Overlaps imap-check-unread-counts-from-an-agent on the seen flag; this recipe's subject is the report (names, counts, uid_validity, owner, reset), that one's is STATUS.
