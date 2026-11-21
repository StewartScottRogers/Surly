<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-20 (batch 6 of the 1,000-recipe plan)

From writing and verifying batch 6 (62 recipes: IMAP and POP3, mail logins, password rotation, MQTT groups, SMB shares, uploads and S3) of the *Surly* cookbook against Surl `9ad60d80c`, Curl `7d167fa10a`, Windows 11. Only findings not already filed are listed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Mail auth | `--auth oauthbearer,xoauth2` with `--mailbox ann:pw` still accepts IMAP `LOGIN` and POP3 `USER`/`PASS` with the password: curl falls back to them and logs in. Expected: `--auth` limits every login path. (Recipes close it with `;protocols=http`.) |
| 2 | Medium | IMAP | Mail delivered while an IMAP session is open is not shown to that session (no `EXISTS` on `NOOP` or the next command); a new session sees it |
| 3 | Medium | Accounts | Removing the last account or proxy account leaves the server open with no login (GET 200). Surl.Api and `remove_account` warn; the control endpoint and `remove_proxy_account` do not |
| 4 | Medium | Log redaction (BL-1425) | A refused Basic proxy login still shows `Proxy-Authorization: Basic <base64>` in the `-v` log |
| 5 | Low | Accounts | Re-adding an existing account: the control endpoint's `POST /v1/accounts` replaces the password (204), while `Accounts.AddAsync` throws `ArgumentException` and `add_account` returns an error. One rule for all three would help |
| 6 | Low | POP3 | `TOP 1` (one argument) answers `+OK`; RFC 1939 requires a message number and a line count |
| 7 | Low | Mailboxes | `--mailbox "x@example.com:pw;protocols=gopher"` starts; `--aihelp` says an unknown scheme exits 2 |
| 8 | Low | Messages | `folders=Sent,Sent` says "member Sent is given twice" (folder, not member); `surl-mcp` LDAP seed refusal reads "The LDIF entries was refused" |
| 9 | Ask | IMAP | No SPECIAL-USE attributes (`\Sent`, `\Drafts`, RFC 6154) on start folders |
| 10 | Ask | Catch-all | Caught copies carry no `Delivered-To`, so a test cannot tell which address a Bcc'd copy was for |
