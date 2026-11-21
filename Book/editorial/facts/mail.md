<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# mail facts (SMTP, IMAP, POP3)
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, 2026-11-19, windows.

## Listen URLs and readiness
`smtp` 25, `imap` 143, `pop3` 110 (TLS twins `smtps`, `imaps`, `pop3s`); TCP, `ready: port`. One surl
with several mail listen URLs runs them over ONE mail store: SMTP delivers, IMAP and POP3 read, a POP3
DELE is gone for IMAP too. Each listen URL needs its own port.

## Server options that matter
- `--allow-anonymous` (loosens, warns): SMTP takes mail and IMAP/POP3 open with no login; any `-u`
  accepted unchecked; every recipient lands in ONE anonymous INBOX; `--mail-domain` NOT applied.
- `--user name:pw`: an account; without `--mail-domain` the recipient's domain is ignored
  (`ann@anything` -> account `ann`) and an unknown recipient is accepted 250 and discarded.
- `--mail-domain example.com` (repeatable): recipient outside every domain -> `550 5.7.1 Relaying denied`
  (even for a logged-in sender). Inside: delivered to its mailbox, or the `--user` named by the local
  part, or accepted 250 and discarded (no address enumeration).
- `--mailbox addr:pw[;protocols=..;quota=..;folders=..]`: mailbox + account named by the WHOLE address
  (`-u ann@example.com:pw`; owner name in MCP/Surl.Api is the address too). Needs `--mail-domain`, must
  be inside one; else exit 2 (`needs --mail-domain`, `is not in any --mail-domain`).
- `--mail-catch-all example.com=catchall@example.com`: unknown addresses of the domain -> that mailbox.
  Exit 2 without `--mail-domain` (`catch-all domain example.com needs --mail-domain`), outside every
  domain (`is not in any --mail-domain`), or target with no mailbox (`surl: (2) option --mail-catch-all:
  catch-all nobody@example.com includes itself`). A real mailbox is not caught. One message to several
  unknown addresses is stored ONCE in the catch-all.
- `--mail-alias a=b,c`, `--mailbox-file` CSV, `--shared-folder`: see `surl --aihelp mail`.
- `--shared-folder team=ann@example.com,bob@example.com`: one folder (reserved owner `:shared`); each member sees
  `Shared/team` (LIST adds a `\Noselect \HasChildren` `Shared`; NAMESPACE `(("" "/")) NIL (("Shared/" "/"))`).
  Members are equals: APPEND, read, STORE (flags are shared: one member's \Flagged shows for the other), MOVE in.
  NO ADDRESS: SMTP to team@ is 250 and discarded; seed_mail / Mail.DeliverAsync to it -> NoSuchAccount.
  Non-member: not in LIST, SELECT `NO [NONEXISTENT]` (curl 67 `Select failed`), APPEND `NO [TRYCREATE]` (curl 25
  `Upload failed`). `DELETE Shared/team` -> `NO [NOPERM] A shared folder cannot be deleted` (curl 21).
  Surl.Api/MCP read it as owner = the member, mailbox `Shared/team`; owner `:shared` -> not found.
- `--mail-alias "support@example.com=ann@example.com,bob@example.com"`: each target gets its own copy; the
  stored message is unchanged (its `To:` still names the alias; no Delivered-To added). An alias may name
  another alias (help@ -> support@ -> ann, bob). One message whose recipients reach a mailbox twice is
  stored there once. An alias is not an account: `-u support@...` -> curl 67. Exit 2 at start: target with
  no mailbox `surl: (2) option --mail-alias: target anne@example.com names no mailbox`; target outside
  every domain `... target ann@elsewhere.org is not in any --mail-domain`; alias that is a mailbox
  `surl: option --mail-alias: alias ann@example.com is also a mailbox`; loop `alias help@example.com
  includes itself`; twice `alias ... is given twice`. Surl.Api `MailAliases`; refusal is
  `SurlServerStartException`, `ExitCode` FailedInit, `Lines[0]` `(2) option --mail-alias: ...`.
  `Mail.DeliverAsync` and MCP start_server obey aliases the same way.
- `--mailbox "app@example.com:apppass;protocols=smtp,http,mqtt"`: the mailbox account then logs in on HTTP
  (Digest) and MQTT too; a mailbox without `protocols=` is refused on HTTP (401) and MQTT (CONNACK 4).
- `--allow-plaintext-auth` (loosens, warns): also offer PLAIN/LOGIN without TLS. Needed by .NET SmtpClient.
- Envelope sender is NOT checked against the login: a logged-in app may `MAIL FROM` any address, even
  outside the domain.

## Secure by default
- No login: SMTP `MAIL` -> 530 (curl 55, `MAIL failed: 530`; SmtpClient `5.7.0 Authentication required`);
  IMAP `NO [AUTHENTICATIONFAILED]` (curl 21); POP3 `-ERR [AUTH]` (curl 8).
- Without TLS only `CRAM-MD5` is offered (SMTP `250 AUTH CRAM-MD5`, POP3 `SASL CRAM-MD5`, IMAP
  `LOGINDISABLED AUTH=CRAM-MD5`). curl and Curl.Api use it; no loosening needed. Wrong password: curl 67.
- SMTP EHLO without TLS ends `250 AUTH CRAM-MD5`; with `--allow-plaintext-auth`
  `250 AUTH CRAM-MD5 OAUTHBEARER XOAUTH2 PLAIN LOGIN`, and curl STILL picks CRAM-MD5 unless
  `--login-options AUTH=PLAIN`. On the default server `--login-options AUTH=PLAIN` (or LOGIN): curl sends no
  AUTH (`* SASL: no overlap between offered and configured auth mechanisms`), exit 67. A client that sends
  `AUTH PLAIN <b64>` / `AUTH LOGIN` anyway gets `538 5.7.11 Encryption required for requested authentication
  mechanism`, right and wrong password alike. Raw SMTP from curl: `curl -s -T plain.smtp telnet://h:p`
  (CRLF fixture ending in QUIT) prints every reply, exit 0. The CRAM challenge decodes to `<hex.digits@surl>`,
  curl's answer to `user <32 hex>` (smtp-log-in-with-cram-md5 has a decode script).
- login_judged events carry `mechanism` (CRAM-MD5, PLAIN), `account`, `is_accepted` (MCP) /
  `LoginJudgedEvent.Mechanism/Account/IsAccepted` (Surl.Api, namespace Surl.Protocol.Abstractions).
  Curl.Api `LoginOptions = "AUTH=PLAIN"` on the default server: CurlException 67, no login judged.
- MQTT on the same surl needs `--allow-plaintext-auth` for any password (CONNACK 5, curl 8); the option
  does not change curl's SMTP choice. HTTP with any `--user` account configured: GET with no login is 401.
- Mailbox accounts (no `protocols=`) log in on smtp, imap, pop3 only (`surl -v` log on start: `* Mailbox ann@example.com:
  logins on smtp, imap, pop3`), so an `http://` listener on a surl with only `--mailbox` accounts serves GET with NO login
  (200; probed 2026-11-20, smtp-test-a-password-reset-mail). `--directory` serves files written after start.

## Client: curl
- Send: `curl --crlf -u app@example.com:pw --mail-from a --mail-rcpt b -T file smtp://host:port/<ehlo-name>`.
  URL path is the EHLO name, ends up in `Received: from <name>`. `--crlf` for LF fixtures.
  Recipient refused: exit 55, `-S` -> `curl: (55) RCPT failed: 550`; `-v` -> `< 550 5.7.1 Relaying denied`.
  Several `--mail-rcpt`, one refused: whole message abandoned (exit 55, nothing delivered);
  `--mail-rcpt-allowfails` sends to the rest, exit 0. `-w "%{response_code}"` -> last reply (250).
- `--next`: curl's exit code is the LAST transfer's (a failed send then a good one exits 0);
  `--fail-early` stops at the first failure. Options do not carry over `--next` (repeat `-u`).
- IMAP: `INBOX;UID=1` whole message (has `Return-Path: <envelope-from>` and a dated `Received` line);
  `;SECTION=TEXT` body; `;SECTION=HEADER.FIELDS%20(FROM%20TO%20SUBJECT)` those fields in message order
  plus a blank line; `INBOX?FROM%20x%20SUBJECT%20%22a%20b%22` -> `* SEARCH 1 2` or `* SEARCH`
  (substring match); `INBOX?ALL`. Missing UID: exit 78. An empty `* SEARCH` is exit 0: a poll loop reads the answer.
- IMAP APPEND: `curl -u .. -T msg.eml imap://h:p/Folder` sends `APPEND Folder (\Seen) {size}`, so appended mail is
  always \Seen. `--crlf` with APPEND: exit 25 `Cannot APPEND with CRLF conversion making size unknown`; use CRLF fixtures.
- `-X "STORE 1 +FLAGS (\Flagged)" imap://h:p/Folder` selects, then prints `* 1 FETCH (FLAGS (\Flagged \Seen))`.
  `-X "MOVE 1 Shared/team" imap://h:p/INBOX` works (prints nothing). `Folder?FLAGGED` searches flags.
- MIME parts: `;SECTION=2` is part 2's body as stored (base64 text, no trailing CRLF); `;SECTION=2.MIME` its own header
  plus a blank line; `-X "FETCH 1 BODYSTRUCTURE"` on `.../INBOX` the whole structure. A part that does not exist:
  exit 0, 0 bytes. Decode in sh: `tr -d '\r' < part | base64 -d`, compare with `cmp -s`.
- POP3: `pop3://h:p/` LIST `1 <octets>` per line (empty maildrop prints a lone CRLF); `/1` RETR;
  `-X "DELE n"`, `-X UIDL`, `-X RSET`; `-X STAT` prints nothing, `-v` shows `< +OK <count> <octets>`.
  `--next` POP3 transfers share one session; DELEs commit at QUIT, and a failing DELE later in the chain
  (exit 8) does not undo earlier ones. Wrong password with n `--next` transfers: n `Login denied` lines.

- SMTP fault on RCPT (2026-11-20): `--fault "error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later;times=1"`; the readiness
  connection does not spend it. curl: exit 55 `curl: (55) RCPT failed: 451`, `-w %{response_code}` 451, curl sends QUIT,
  nothing stored. `--retry N` alone does NOT retry an SMTP 451 (`451 0`); `--retry-all-errors` does (stderr `Warning: Problem
  (retrying all errors). Retrying in 1 second. 1 retry left.`, `250 1`) and retries a 550 just the same (`550 2`).
  `surl -v` log: `RCPT answered with the chosen error of fault rule f1; nothing was stored.`, `Message stored: 202 bytes for 1
  recipients`, HTTP `GET /x: 200, 43 bytes of <path>`, `GET /x: 404, nothing exists at <path>`.
- surl adds `Return-Path` and `Received` at the top of a stored message, never `Date` or `Message-ID`.

## .NET
- Curl.Api sends SMTP: `new CurlRequest(HttpMethod.Put, smtpUrl + "example.com")`
  with `Mail = new CurlMailOptions { MailFrom = .., MailRecipients = [..] }`, `Authentication`, and
  `Body` as bytes or `CurlContent.FromStream(...)` -> `StatusCode` 250 (CRAM-MD5, no loosening). Since Curl 7d167fa10a
  (BL-5594) a byte body uploads too, with PUT or POST; before, it was POSTFIELDS (PUT sent a literal `PUT`, 500; POST a VRFY).
  `System.Net.Mail.SmtpClient` also works as the app, with
  `AllowPlaintextAuthentication = true` (SmtpClient has no CRAM-MD5; without it: SmtpException
  `MustIssueStartTlsFirst ... 5.7.0 Authentication required`).
- Curl.Api POP3 delete: `new CurlRequest(new HttpMethod("DELE"), pop3Url + "1")`. CurlClient keeps the
  connection for reuse, so QUIT (and the deletion) happens only when the client is disposed.
- SmtpClient, refused recipient: `SmtpFailedRecipientException`, `FailedRecipient` `<carol@elsewhere.org>`,
  message `Mailbox unavailable. The server response was: 5.7.1 Relaying denied`. Mixed list: it STILL
  delivers to the accepted recipients, then throws (curl abandons the message instead).
- Curl.Api reads IMAP/POP3 fine: `CurlRequest(HttpMethod.Get, url)` + `CurlAuthenticationOptions`; a
  missing UID throws `CurlException` 78 `Remote file not found`.
- Curl.Api CANNOT IMAP APPEND at Curl 7d167fa10a (BL-5594 left imap out; BL-5610 fixes it later): Put + byte body ->
  CurlException 21 QuoteError; Put + `CurlContent.FromStream(stream, length)` -> 25 `Cannot APPEND with unknown input file size`.
  Nor STORE/MOVE: `new HttpMethod("STORE 1 +FLAGS (\Flagged)")` throws FormatException (one token only; BL-5612 adds
  CustomRequest later). Raw TcpClient IMAP with CRAM-MD5 by hand
  (hex HMACMD5(password, challenge)) works: imap-test-a-shared-team-folder-in-csharp.
- SmtpClient, RCPT answered 451 (one recipient): plain `SmtpException`, `StatusCode` LocalErrorInProcessing, message `Error in
  processing. The server response was: 4.3.0 try later`; a 550 is `SmtpFailedRecipientException` MailboxUnavailable.
- SmtpClient + `new Attachment(stream, "report.csv", "text/csv")`: part 2, base64, `Content-Type: text/csv;
  name=report.csv`, `Content-Disposition: attachment` with no filename. `Convert.FromBase64String` skips CRLFs.
- `server.OnEvent(new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail }, cb)`: one
  `StoreChangedEvent` per delivery (`Change` Delivered, `Name` `owner/INBOX/;UID=n`), in order; alias to two -> two;
  a discarded address -> none. `server.ResetAsync()` empties every mailbox and keeps them.
- Surl.Api: `MailDomains`, `Mailboxes` ("addr:pw"), `MailCatchAlls` ("domain=addr"), `MailAliases`,
  `Accounts` (`SurlAccount`), `AllowPlaintextAuthentication`. `BoundUrls` are `Uri` (use `$"{u}"`).
  `server.Mail.DeliverAsync/ListMailboxesAsync(owner)/ListMessagesAsync/ReadMessageAsync`;
  `SurlMailboxInfo.MessageCount`. Unknown owner: `SurlNotFoundException`.
- File-based apps: `JsonSerializer.Serialize(string)` throws (reflection serialization off); print
  `JsonElement.GetRawText()` instead.

## MCP
- `start_server(listen_urls, arguments)` with mail options in `arguments`; refusal at start is a tool
  error, structured `exit_code` 2, `exit_code_name` FailedInit, `lines` [message, try line].
- `seed_mail(server_id, to, message)` -> `{"delivered":true}`; obeys domains: outside -> tool error
  `Mail: Mail for x was refused: RelayingDenied.`; unknown in-domain with no catch-all -> `NoSuchAccount`
  (SMTP would accept and discard).
- `list_mailboxes(server_id, owner)` -> `mailboxes[{name, message_count, unseen_count, uid_validity}]`;
  unknown owner -> tool error `No mailboxes for x.` `list_messages(server_id, owner, mailbox, after_uid, limit)`,
  `read_message(..., uid)` (sets no flag). No delete-message tool: delete over POP3/IMAP, or `reset_server`.
- `get_exchanges(server_id, scheme)` -> `exchanges[].chunks[{direction sent|received, data}]`, byte for
  byte: find `RCPT TO:<x>` and the reply after it.
- Deliveries are events: `get_events`/`wait_for_event` with `kinds [store_changed]`, `store mail` give
  `{kind store_changed, change delivered, name "ann@example.com/INBOX/;UID=1", byte_count, exchange_id}`,
  one per target mailbox (an alias to two -> two events; a discarded unknown address -> none).
  `name: "ann@example.com"` matches as a segment prefix. `account` matches the SENDER's login exchange,
  not the recipient. `wait_for_event` timeout -> `{"timed_out":true}` (not a tool error); an event already
  kept returns at once; later waits still return it.
- `read_message` -> `text` (the message); `read_retained_message` -> `{topic, qos, text}` or
  `{"retained":false}`.
- `add_account(user, password)` -> `{"user":..}`; the account logs in on every scheme but has NO mailbox:
  IMAP SELECT INBOX -> `NO [NONEXISTENT]` (curl 67), seed_mail -> `NoSuchAccount`, SMTP to it 250 and
  discarded (also for `user@example.com` under `--mail-domain`). `remove_account` keeps a start-time
  `--user`'s mailbox (mail is still delivered to it; re-adding the name gives it back); `reset_server`
  drops runtime accounts and empties mail. Removing the last account makes the server need no login (GET
  with none -> 200). Duplicate add: tool error `user task2 already has an account`; unknown remove:
  `server s1 has no account nobody`. Accounts store events: `account_added`/`account_removed`, `name`.
- curl-mcp `curl(args)`: `exitCode`, `standardOutput.content`, `standardError.content`; non-zero exit is
  a tool error (`isError`). `-T file` reads from CURL_MCP_ROOT.

- `add_fault(server_id, fault)` on a mail server: `error;scheme=smtp;command=RCPT;reply=250 OK` -> tool error `option --fault:
  key reply does not fit the smtp error shape`. `list_faults` `remaining` is JSON null for a rule with no `times`.
- `--mail-catch-all` start warning (in `log_lines`): `surl: warning: --mail-catch-all: example.com keeps mail to every unknown
  address, which would otherwise be discarded`.
- `put_file(server_id, path, text)` publishes a page an `http://` listener of the same server serves at once.

## Gotchas
- Fixture line ends: LF fixtures need `--crlf`; CRLF fixtures in mcp recipes avoid it but it is harmless.
- `Received:` carries the client's EHLO name (SmtpClient sends the machine name): never match it exactly.
- `curl -T "order[01-10].eml"` (a glob) sends ten messages in one SMTP session; good for `seed:`.
  `-w "%{num_connects}"` shows it: 1, then 0. A missing file in a `-T` glob or `{a,b}` list stops curl
  there: files before it sent, after it never, exit 26, `-w` prints `000` for it.
- `curl -v` on Windows writes lines ending CR CR LF; verify reads them as two newlines. Match with `\s+`.
- IMAP `-X "STATUS INBOX (MESSAGES)"` on `imap://h:p/` -> `* STATUS INBOX (MESSAGES 3)`.
  `INBOX;UID=[1-3];SECTION=...` (a curl URL glob) fetches three messages.
- curl prints a progress meter on stderr for multi-transfer IMAP/POP3 commands without `-s`.
- Ad hoc probes from Git Bash: start a background surl with `< /dev/null`, or it exits (code 1) after a client or two.
- No-login "does it answer" checks (2026-11-20): `curl -X NOOP smtp://h:p/` exit 0, `%{response_code}` 250;
  `curl -X NOOP imap://h:p/` exit 0 but `%{response_code}` 000 (curl gives no IMAP code); plain `curl imap://h:p/`
  with no login is exit 21. FTP always logs in first: anonymous 530, exit 67, code 530. Curl.Api
  `new CurlRequest(new HttpMethod("NOOP"), url)`: SMTP and IMAP both `StatusCode` 0 (curl CLI says 250 for SMTP:
  reported); FTP throws `LoginDenied`. surl -v logs `#N < NOOP

` (SMTP), `#N < A002 NOOP

` (IMAP).

## Recipes
smtp-send-and-read-it-over-imap, pop3-retrieve-and-delete-a-message, smtp-serve-a-mail-domain,
smtp-check-the-mail-an-app-sent (+ -in-csharp, -from-an-agent), smtp-catch-every-address-in-one-mailbox
(+ twins), smtp-fail-a-test-on-a-wrong-recipient (+ twins), pop3-empty-mailboxes-after-a-run,
surl-seed-a-mailbox-before-a-test (+ twins), surl-hand-bound-ports-to-the-app (+ twins),
smtp-route-mail-through-aliases (+ twins), smtp-log-in-with-cram-md5 (+ twins),
surl-stand-up-mail-files-and-messages (+ twins), smtp-send-a-batch-of-mails-from-files,
smtp-wait-for-an-apps-mail, surl-give-each-task-its-own-account, surl-smoke-test-every-listener (+ twins), surl-run-a-test-server-in-ci (+ twins),
imap-test-a-shared-team-folder (+ twins), smtp-check-a-mail-attachment (+ twins), imap-poll-until-the-mail-arrives,
smtp-assert-mail-in-an-xunit-fixture, smtp-collect-mail-as-it-arrives,
smtp-survive-a-temporary-rcpt-failure (+ twins), smtp-test-a-password-reset-mail (+ twins), smtp-review-an-apps-mail-against-rules.
