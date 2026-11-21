<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-give-each-task-its-own-account
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Nearest honest recipe: accounts from add_account work for HTTP but get no mailbox, so per-task IMAP mail is impossible with this build (case imap-has-no-mailbox); said in the Discussion.
2. Program finding (surl-mcp add_account / Surl.Api Accounts.Add): `add_account task-1` then `curl -u task-1:pw imap://h:p/INBOX?ALL` -> `NO [NONEXISTENT] Mailbox does not exist`, curl 67; `seed_mail to task-1@example.com` -> NoSuchAccount. Expected: an account added at run time gets an INBOX like a `--user` given at start, since the tool says it logs in on every scheme.
3. Program note: remove_account keeps a start-time account's mailbox and mail is still delivered to it after removal; isolation of mail between successive users of one name needs reset_server.
4. Accounts do not partition the content store (case files-are-shared); the recipe says so and has the agent delete each task's files.
5. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: no change. add_account still gives no mailbox (case imap-has-no-mailbox passes); no Surl commit in the range touches it.
6. Re-pinned to Surl a423807a5d / Curl 0680b97577: an account from add_account now gets an INBOX (finding 2 is fixed), so the IMAP case became imap-mailbox-per-task: mail seeded to task-1 is task-1's alone, the login is refused after remove_account, and the mailbox survives removal, so a re-added task-1 sees the old mail (as finding 3 said of start-time accounts). The Discussion no longer calls per-task mail impossible.
