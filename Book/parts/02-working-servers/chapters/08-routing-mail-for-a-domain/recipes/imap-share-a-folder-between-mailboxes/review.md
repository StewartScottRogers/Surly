<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-share-a-folder-between-mailboxes
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Differs from imap-test-a-shared-team-folder (flags, mail to the folder's name) by membership: a second folder shared with a @group, each mailbox's LIST, and start-time refusals.
3. A non-member's STATUS of a shared folder and of a folder never made get the same NO [NONEXISTENT]; claimed with a case.
