<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-19 (batch 2 of the 1,000-recipe plan)

From writing and verifying batch 2 (61 recipes: mail, SMB, FTP, DICT, HTTP ranges and globs, limits and faults, seeding, tokens, accounts) and from fixing 11 recipes for Linux and macOS in CI. Follows `2026-11-19T1942-Surl-bug-report.md`, whose items still stand.

**Build tested:** Surl at commit `763754785`: `surl 1.0.0`, `surl-mcp`, `Surl.Api 1.0.0`. Windows 11; Linux and macOS through the cookbook's CI (GitHub `ubuntu-latest`, `macos-latest`). Client: the Curl port at `b9ca725077`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | `surl-mcp` accounts | An account added with `add_account` can log in over IMAP but has no INBOX (`NO [NONEXISTENT]`), and `seed_mail` refuses it with `NoSuchAccount`; a `--user` given at start has one |
| 2 | Medium | `surl-mcp` accounts | `remove_account` on a start-time account keeps its mailbox, and mail is still delivered to it |
| 3 | Medium | TLS (macOS) | `surl --tlsv1.3` starts on macOS, then fails every handshake (curl exit 35). Surl ADR-0010 documents that the macOS TLS library has no TLS 1.3 server; surl should refuse at start (exit 2 or 58) |
| 4 | Low | HTTP | A logged-in `PUT` to `/.surl/x` gets 409; 403 or 404 would say the path is reserved |
| 5 | Low | `surl-mcp` snapshots | `restore_server` given another server's snapshot answers "only its newest 4 snapshots are kept"; it should say the snapshot belongs to another server |
| 6 | Low | Surl.Api | No property for `--max-request-head`; it needs `AdditionalArguments` (as do the TLS range and `--cert-type`, report 1942 #10) |

### 1. `add_account` and mail
`add_account task-1` (password `pw`) through `surl-mcp` on a server with an IMAP listener, then `curl -u task-1:pw "imap://127.0.0.1:PORT/INBOX?ALL"`: the login is accepted, then `NO [NONEXISTENT]`, curl exit 67. `seed_mail` to `task-1` is refused `NoSuchAccount`. **Expected:** the same mailbox a `--user task-1:pw` at start would have. The cookbook's per-task-accounts recipe is limited to HTTP until this changes.

### 3. TLS 1.3 on macOS
On `macos-latest`, `surl --tlsv1.3 --cert server.pem --key server.key https://127.0.0.1:PORT/` starts and prints its listening line; `curl --cacert server.pem https://127.0.0.1:PORT/` exits 35. Without `--tlsv1.3`, TLS 1.2 is served. **Expected:** a start-time refusal naming the platform, as for an impossible version range.
