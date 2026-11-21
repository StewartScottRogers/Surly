<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-add-an-account-per-test
Verdict: accept with a narrowed task
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. The plan's task (accounts so no test can read another's files or mail) is not possible with these builds: accounts divide logins only. The recipe shows that honestly: test-2 reads test-1's file (200), and an added account has no IMAP mailbox (Select failed). Files are deleted per test; mailboxes come from Mailboxes at start.
2. Observed, not claimed: SMTP accepts RCPT for an added account (or any unknown in-domain address) with 250 and discards the mail, while Mail.DeliverAsync to it throws NoSuchAccount (matches facts/mail.md for unknown addresses).
3. Duplicate Add throws ArgumentException 'User test-1 already has an account.'; the finally block is justified by that case.
Re-pinned to Surl a423807a5d / Curl 0680b97577: Accounts.Add now gives an account its mailboxes (BL-1452), so the added account reads its INBOX; after Remove its open connection keeps its login (ADR-0132 decision 8) and a new client's login is refused. no-mailbox-for-an-added-account became mailbox-for-an-added-account.
