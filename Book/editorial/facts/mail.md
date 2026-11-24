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
- Since Surl a423807a5d: an account from `add_account` HAS an INBOX: `seed_mail to task-1@example.com` ->
  `{"delivered":true}`, `INBOX?ALL` -> `* SEARCH 1`, another added account's INBOX `* SEARCH`; after
  `remove_account` login 67, and re-adding the name finds the old mail (mailbox outlives the account).
  The lines below are the old behaviour (Surl 9ad60d80c and before).
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

## Logins by token and APOP (2026-11-20, windows probed)
- `--mailbox "viewer@example.com:pw;protocols=imap"`: the account logs in on IMAP (and imaps) only. SMTP and POP3 give
  curl 67 `Login denied`; SMTP `-v` shows `AUTH CRAM-MD5` then `535 5.7.8 Authentication credentials invalid`, the reply a
  wrong password gets. Delivery to the mailbox still works. `protocols=http` makes a mailbox that takes mail and logs in on no
  mail protocol (IMAP/POP3 curl 67 `Access denied.`, SMTP 67). `protocols=gopher` was accepted at start (no exit 2).
- A token is an account with an empty user name (`--user :tok`). IMAP: curl `-u ann@example.com --oauth2-bearer tok` over
  imaps sends `AUTHENTICATE OAUTHBEARER` (`--login-options AUTH=XOAUTH2` -> `AUTHENTICATE XOAUTH2`); logins judged as account
  `bearer token`. The user name only picks the mailbox read (bob with ann's token: UID 1 exit 78; unknown name: empty LIST).
  Over plain imap CAPABILITY has no OAUTHBEARER/XOAUTH2 (`STARTTLS LOGINDISABLED AUTH=CRAM-MD5`), curl sends nothing, exit 67;
  raw `AUTHENTICATE XOAUTH2 <b64>` gets `NO [PRIVACYREQUIRED] Encryption required` right or wrong token, no login judged.
  With `--ssl-reqd` (STARTTLS) the token works. `--allow-plaintext-auth`: plain imap accepts it, wrong token still 67.
  `--auth oauthbearer`: only `AUTH=OAUTHBEARER` advertised; AUTH=XOAUTH2 curl 67. XOAUTH2 base64 =
  `user=<name>\x01auth=Bearer <tok>\x01\x01`. SMTP over smtps: `AUTH OAUTHBEARER` -> `334 ` -> payload -> 235; with
  `AUTH=XOAUTH2` curl sends `AUTH XOAUTH2` then the payload on its own line. Stored mail `Received: ... with ESMTPSA`.
- `--auth oauthbearer,xoauth2` does NOT close IMAP `LOGIN` (curl falls back to it: `A002 LOGIN ann pw` OK over imaps) or
  POP3 USER/PASS over pop3s; SMTP password logins are refused. A wrong password over imaps there: curl 67 `Access denied.`.
- curl keeps one IMAP session across `--next` and CurlClient reuses a logged-in connection: a second request with a wrong
  token on the same session still succeeds. One client / one curl run per login attempt.
- POP3 APOP: `--auth apop` (alone) greets `+OK surl ready <hex.digits@surl>`, curl logs in with APOP by itself
  (`> APOP ann@example.com <md5 of timestamp+password>`), wrong password curl 67; raw `APOP` with a bad digest
  `-ERR [AUTH] Authentication failed`; `USER` -> `-ERR [AUTH] Encryption required`; CAPA has no SASL line. Default server:
  greeting `+OK surl ready` (no timestamp), raw APOP `-ERR Unsupported authentication mechanism`. `--login-options AUTH=APOP`:
  curl exit 3. `--auth apop,cram-md5`: timestamp kept, curl picks CRAM-MD5. Surl.Api `AuthenticationMethods = ["apop"]`
  (null by default, assign a list); `LoginJudgedEvent.Scheme` exists. `SurlExchangeRecord.Sent/Received` are the server's
  view (Sent = server to client).
- Surl.Api `SelfSigned = true` + `server.SelfSignedCertificatePem` + `CurlTlsOptions{Origin=CurlTlsPeerOptions{CertificateAuthorityBlob}}`.
  `new SurlAccount("", "tok")` is the token account. Curl.Api `XOAuth2Bearer` + `UserName` works for IMAP and SMTP.
  MCP `wait_for_event` answers `{"event":{...,"sequence":n,"name":..}}`; `after_sequence` skips earlier ones.

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

## Counts, folders, TOP and the catch-all (2026-11-20, Surl 9ad60d80c, Curl 7d167fa10a, windows)
- Bounded wait for asynchronous delivery on the console, no sleep: `curl --retry 10 --retry-all-errors --retry-delay 1
  -X "FETCH 3 FLAGS" imap://h:p/INBOX` (missing message: `BAD Invalid message sequence number`, exit 21; FETCH FLAGS
  sets no flag), or `... -X "LIST 3" pop3://h:p/` (exit 8), or `"imap://h:p/INBOX;UID=3"` (exit 78). Works as a `seed:` line
  and after `--next` in a case. On Windows 30 send-then-STATUS loops never lagged; the guard is for macOS CI.
- IMAP CAPABILITY: `IMAP4rev1 SASL-IR UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600` (+ auth). No
  SPECIAL-USE: LIST shows no \Sent/\Drafts; `LIST "" "*" RETURN (SPECIAL-USE)` -> exit 21.
- STATUS: `-X "STATUS INBOX (MESSAGES UNSEEN)" imap://h:p/` -> `* STATUS INBOX (MESSAGES 3 UNSEEN 3)`, sent as A003 with
  no SELECT. Items MESSAGES, UNSEEN, RECENT (always 0), UIDNEXT, UIDVALIDITY; `SIZE`/`DELETED` (IMAP4rev2) -> `BAD Invalid
  arguments`, exit 21. Unknown folder -> `NO [NONEXISTENT] Mailbox does not exist`, exit 21.
- curl fetches with `UID FETCH n BODY[...]` (never BODY.PEEK): `INBOX;UID=1` AND `;SECTION=HEADER.FIELDS%20(SUBJECT)`
  both set \Seen. `-X "STORE 1 -FLAGS (\Seen)"` unsets it. POP3 RETR/TOP do NOT set IMAP \Seen. `INBOX?UNSEEN` lists them.
- `--mailbox "a@x:pw;folders=Sent,Drafts,Archive"`: LIST `INBOX` first then by name, all `\HasNoChildren`, empty. Space kept
  (`"Sent Items"` quoted in LIST); `Archive/2026` alone implies `(\Noselect \HasChildren) "/" Archive`; `%2C` -> a comma in
  the name. `folders=Sent,Sent` -> exit 2 `surl: option --mailbox: member Sent is given twice`; `folders=` -> exit 2
  `key folders needs a value`; `folders=INBOX` accepted silently. Client may DELETE a start folder; a restart (memory
  store) brings it back and empties the rest. ResetAsync / reset_server keep the folders. Surl.Api/MCP list ordinal order.
- POP3: `-X UIDL` -> `n <uidvalidity>.<uid>` per line; `-X "UIDL 2"` prints nothing (`-v`: `< +OK 2 1795220321.2`).
  `-X "TOP 2 0"` -> header + blank line; `TOP 2 2` + two body lines; `TOP 2 99` whole message. `-X TOP pop3://h:p/2%200`
  sends `TOP 2 0` (curl appends the decoded path to a custom command: the way to pass two arguments from Curl.Api).
  Missing message: `-ERR No such message`, exit 8 (Curl.Api CurlException 8 `Weird server reply`). `TOP 1` (no count) is
  answered +OK (lenient). `--next` POP3: DELE then UIDL in one curl still lists old numbers; a new curl renumbers, IDs kept.
- Catch-all: alias and real mailboxes win over it; address match ignores letter case (`Ann@Example.COM` -> ann). Per
  domain: an unknown address of another served domain with no catch-all is discarded (`surl -v`: `Mail for x discarded:
  no such account`, then `Message stored: ... for 0 recipients`). Caught copy has no Delivered-To/X-Original-To: an
  envelope-only (Bcc) address cannot be searched. Target may be in another served domain. Same domain twice -> exit 2
  `catch-all domain example.com is given twice`.
- Curl.Api 0.2.0 cannot send IMAP STATUS (one-token HttpMethod: `FormatException: The format of the HTTP method is
  invalid.`); count with `INBOX?UNSEEN` (SEARCH) instead. `new HttpMethod("UIDL")`, `("TOP")` + path `n%200`, `("RETR")` + `n` work.
- Surl.Api `SurlMailboxInfo.UnseenCount`; `ListMessagesAsync(owner, mailbox)` returns `SurlMessagePage` (`.Messages`,
  `.HasMore`); `DeliverAsync` adds no Return-Path/Received. ReadMessageAsync / read_message never set \Seen.
- A CurlClient keeps its IMAP connection with the folder selected and curl skips the re-SELECT, and surl sends no
  untagged EXISTS: mail delivered after that SELECT is invisible to the same client (`INBOX?ALL` -> `1 2 3` after a 4th);
  a new CurlClient sees it. Use a fresh client or the store for counts after new mail.
- Curl.Api SELECT of a missing folder (`Archive?UNSEEN`): CurlException 67 `Select failed`. Store reads for an unknown
  owner: SurlNotFoundException `No mailboxes for x.`
- Curl.Api `new HttpMethod("UIDL")` + path `2` hung (>60 s); avoid. `DELE` + path `1` then dispose the client: QUIT
  commits; the same client still lists the old numbers.
- reset_server (and presumably ResetAsync) RESTORES a start folder a client deleted, as well as emptying mailboxes.

## IMAP faults, several recipients, kept mail, alias forwarding (2026-11-20, Surl 9ad60d80c, Curl 7d167fa10a, windows)
- IMAP fault: `--fault "error;scheme=imap;command=FETCH;account=ann@example.com;reply=NO [SERVERBUG] fetch failed;times=1"` (`account=` is the whole
  address; a rule per account lets one server serve many cases). curl's `UID FETCH n BODY[...]` answered NO -> exit 78 `curl: (78) Remote file not found`
  (BAD too); a SEARCH answered NO -> exit 21 `Quote command returned error`; `command=SELECT` -> exit 67 `Select failed`. `--retry 2` alone: no retry
  (`-w %{num_retries}` prints 0, exit 78); `--retry-all-errors`: one retry, exit 0. `curl -v` shows `A004 NO [SERVERBUG] fetch failed` after `A003 OK
  [READ-WRITE] SELECT completed`. `-v` log: `FETCH answered with the chosen error of fault rule f1.`; start warning `surl: warning: --fault: N fault rule(s)
  make this server misbehave on purpose` once for all N. `reply=250 OK` for imap: exit 2 `surl: option --fault: key reply does not fit the imap error shape`.
- Surl.Api: `SurlFault.ProtocolError("NO [SERVERBUG] fetch failed") with { Scheme = "imap", Command = "FETCH", Times = 1 }`; `Faults.Add(rule)` returns the id string
  (`f1`), `Remove(id)` true; `Log` (StringWriter) with `LogLevel.Verbose` (`using Surl.Output;`) holds the warning and the fault line; `server.Exchanges[i].Sent` is
  `byte[]` (server to client). Curl.Api: NO to the fetch is `CurlException`, `Code` `RemoteFileNotFound` (78); NO to SELECT is `LoginDenied` (67) `Select failed`. MCP curl
  tool: a failing answer still carries `-w` output in `standardOutput`; `start_server` `log_lines` holds the fault warning; `get_events` `fault_applied` gives `rule_id`,
  `fault_kind` `error`, `stage` `command`; `list_faults` `remaining`.
- One message, several `--mail-rcpt`: each recipient gets a copy, the stored message is unchanged (its `To:` names whom the headers name; no header names a Bcc-like
  recipient), `-v` log `Message stored: N bytes for 3 recipients` once. A refused recipient (outside every domain): exit 55 `RCPT failed: 550`, nothing delivered, also to
  those named before it; `--mail-rcpt-allowfails`: exit 0, the rest delivered. An unknown in-domain address: 250 (`-w %{response_code}`), discarded, no event. An address
  named twice is delivered once. Curl.Api `CurlMailOptions.MailRecipientAllowFails = true`; a refusal is `CurlException` `SendError` (55) `RCPT failed: 550`. MCP `get_exchanges`
  `received` chunks: three `RCPT TO:<` and one `DATA`. Proving "not delivered twice" needs a marker: deliveries are in order, so wait for the event of a mailbox named last.
- Kept mail: `--directory data` (must exist; a fixture `data/readme.txt` makes it) keeps `data/.surl/lock` and `data/.surl/mail/{index,messages/}`; flags (\Flagged, \Seen),
  UIDNEXT and the messages survive a `restart:` (kill) and a clean dispose; the lock file stays after a clean stop and the next start ignores it. Second surl on the folder: exit 124
  `surl: (124) Directory data is in use by another surl process`; Surl.Api `SurlServerStartException.ExitCode` `DataDirectoryInUse`, `Lines[0]` `(124) Directory <full path> is in use by
  another surl process`; MCP `start_server` tool error `exit_code` 124 `DataDirectoryInUse`. Without a directory a restart empties the mail and the `--mailbox` mailboxes return.
  MCP: `stop_server` then a second `start_server` with the same `arguments` (`--directory` inside SURL_MCP_ROOT, made by the program); a new server's event sequence starts empty.
  Surl.Api: `Mail.ListMailboxesAsync(owner)` `UnseenCount` 1 -> 0 after a Curl.Api IMAP fetch, still 0 after a restart.
- Alias, ground rules (smtp-forward-mail-with-an-alias): Surl.Api `AdditionalArguments.Add("--mail-alias"); AdditionalArguments.Add("support@example.com=ann@example.com,bob@example.com")` is the
  same as `MailAliases.Add(...)`; refusals `refused.Lines[0]`: `option --mail-alias: alias ann@example.com is also a mailbox` (no `(2)` prefix), `(2) option --mail-alias: target anne@example.com names no
  mailbox`, `(2) option --mail-alias: target ann@elsewhere.org is not in any --mail-domain`; MCP the same with `exit_code` 2. A login as the alias: curl 67 / `LoginDenied`.
  The alias's stored copy has `To:` as sent, so the alias does not appear in the target's mailbox name.

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
smtp-survive-a-temporary-rcpt-failure (+ twins), smtp-test-a-password-reset-mail (+ twins), smtp-review-an-apps-mail-against-rules,
imap-check-unread-counts (+ twins), imap-start-a-mailbox-with-folders (+ twins), pop3-peek-at-headers-with-top (+ twins),
smtp-catch-mail-for-unknown-addresses (+ twins).
imap-keep-a-mailbox-off-smtp, imap-log-in-with-an-oauth-token, pop3-log-in-with-apop, imap-log-in-to-mail-with-a-token (all with twins).

- Do not use Curl.Api `UseSsl.All` (or Try/Control) in a recipe until Curl BL-5640 lands: on `ftp://` it sends USER and PASS in clear with no AUTH TLS, and on `imap://` the STARTTLS login fails 67. Use the implicit-TLS scheme (`ftps://`, `imaps://`, `pop3s://`, `smtps://`) instead (Curl session, 2026-11-20).

- Decided by Surl (2026-11-20): IMAP `LOGIN` and POP3 `USER`/`PASS` stay outside `--auth` and are governed by `--allow-plaintext-auth` alone (Surl ADR-0049 decision 1); `;protocols=` closes them. POP3 `TOP n` with no line count means 0 lines (ADR-0056 decision 5). A repeated list member is reported as "member X is given twice" everywhere (ADR-0101). Only `NOOP` and the commands ADR-0055 names report new mail (`EXISTS`) to an open IMAP session; FETCH, STORE, SEARCH, COPY and MOVE do not.
smtp-forward-mail-with-an-alias, smtp-keep-mail-across-restarts, smtp-send-to-several-recipients, imap-fail-one-command (all with twins).

## SMTP faults on recipients and connections, mail events (batch-08, 2026-11-21, Surl 9ad60d80c, Curl 7d167fa10a, windows)
- `--fault "error;scheme=smtp;command=RCPT;path=bob@example.com;reply=550 5.1.1 mailbox unavailable"`: on an SMTP RCPT the `path` key is the
  recipient's address, so the rule refuses that one address and no other (`times` optional). Surl.Api `SurlFault.ProtocolError(reply) with { Scheme
  = "smtp", Command = "RCPT", Path = "bob@example.com" }`. A reply with no text (`reply=451`, `ProtocolError("451")`) is refused at start: exit 2
  `surl: option --fault: key reply does not fit the smtp error shape` (Surl.Api `SurlServerStartException`, `ExitCode` FailedInit, `Lines[0]`
  `option --fault: ...`; MCP start_server tool error, `exit_code` 2, `exit_code_name` FailedInit, `lines[0]`). `times=0` -> exit 2 `key times must be a whole number from 1 to 1000000`.
- RCPT answered 451 with several recipients: curl abandons the message at the refused RCPT (exit 55, `451`, nothing stored, those named after it
  never tried) unless `--mail-rcpt-allowfails` (exit 0, `250`, delivered to the accepted ones; `Message stored: N bytes for 1 recipients`). Curl.Api: `CurlException`
  `SendError` (55) `RCPT failed: 451`; with `MailRecipientAllowFails = true` the send returns 250. A `times=2` rule spent over two sends: the 3rd is accepted.
- curl URL glob `smtp://h:p/{first,second}` with `-T file`: two sends in one command (`-w` prints per transfer, exit = the last's); the path names the EHLO name, so the
  stored message's `Received: from second` shows which send was stored.
- `drop;scheme=smtp;times=1` (accept stage, before the greeting): verify's readiness connection would spend it, so the server uses `ready: none` (schema 2 recipe) and
  a `seed:` line waits for another listener on the same surl, listed LAST so the SMTP port is bound by then: `curl -s --retry 30 --retry-connrefused --retry-delay 1
  -X NOOP imap://127.0.0.1:{{port2}}/` (exit 0 with no login). curl: exit 56 (`curl: (56) response reading failed (errno: 0)` on Windows; match `curl: (56)`), `-w
  %{response_code}` `000`; `--retry N` alone does not retry it (`000 0`), `--retry 1 --retry-all-errors --retry-delay 1` does (`250 1`). Curl.Api: `CurlException`
  `RecvError` (56). In-process (Surl.Api) and MCP `add_fault` have no readiness connection, so `Times = 1` is safe there.
- `drop;scheme=smtp;stage=command;command=DATA;times=1`: the drop comes before the message is accepted (nothing stored; curl exit 56, `%{response_code}` still 250
  from the earlier reply; the retry stores one copy; log `DATA dropped the connection, unanswered, by fault rule f1.`). `command=QUIT`: after `Message stored` and the 250;
  curl exits 0, no retry, one copy (a sender that resent would leave two). `command=MAIL`: curl 56, nothing stored. The default log has `* Exchange N opened: smtp from ...`
  per connection, also for the accept-dropped one: `grep -c "opened: smtp"` counts connections.
- SmtpClient, one refused recipient of three (550, `path=`): `SmtpFailedRecipientException`, `FailedRecipient` `<bob@example.com>`, StatusCode MailboxUnavailable, the others
  ARE delivered. Two refused: `SmtpFailedRecipientsException` (`InnerExceptions` each with `FailedRecipient`), the third delivered. A 451 for one of three: plain
  `SmtpException` LocalErrorInProcessing, message `Error in processing. The server response was: 4.3.0 try later`, NOBODY gets the message. Only recipient refused: nothing delivered.
- Surl.Api events: `server.OnEvent(query, (e, ct) => ValueTask)` calls run one at a time in order; a throwing callback does not stop the delivery, other callbacks or the
  next delivery; Dispose stops that callback only. `StoreChangedEvent`: `Change` Delivered (SMTP), Seeded (`Mail.DeliverAsync`, name `ann@example.com/INBOX/;UID=1`), Reset
  (`ResetAsync`, empty `Name`), also Written, Deleted ...; `ByteCount` equals the stored message length (with Return-Path/Received), `ExchangeId` set for SMTP deliveries,
  `Account` EMPTY for a delivery, `Sequence`, `At`. `SurlEventQuery.Name = "ann@example.com"` matches by segments (bob's mail does not run it). A callback may call
  `server.Mail.DeliverAsync` (a Seeded event; filter by Name to avoid reacting to itself). `FindEvents` needs `RecordExchanges = true` (else InvalidOperationException);
  a witness callback is the alternative. Mail to an in-domain unknown address: no event. `server.WaitForEventAsync(query, TimeSpan, ct)` exists.
- MCP: `list_messages` -> `{messages:[{uid,size,flags:["\Seen"],internal_date}],has_more}`; the flag string is `\Seen` (one backslash); a curl IMAP fetch of `INBOX;UID=1` sets it, an
  APPEND (`-T file imap://h:p/Sent`) stores `\Seen`, seed_mail stores no flag. `list_mailboxes` lists mailboxes in ordinal name order with `uid_validity` positive and stable (Archive,
  Drafts, INBOX, Sent for `folders=Sent,Drafts,Archive`); bare `ann` or unknown owner: tool error `No mailboxes for ann.`; `reset_server` keeps the folders, empties the mail.
- MCP POP3 deleting: no delete tool; `-X "DELE 1" pop3://h:p/` through the curl tool deletes at the end of that curl run (QUIT); the store shows it at once, and a `store_changed`
  event `change` `deleted`, `name` `ann@example.com/INBOX/;UID=1` follows (wait_for_event after_sequence). `DELE 1 --next LIST --next RSET` in one run deletes nothing. After a deletion
  POP3 numbers the survivor 1 while its UID is 2. POP3 LIST octets equal `list_messages` `size` for CRLF messages. `DELE 5` missing: curl 8, `< -ERR No such message` (with -v).
  `RETR` text equals `read_message` `text` (CRLF). One surl-mcp with `pop3://...:0` and `imap://...:0` lists both in `bound_urls` in that order; IMAP `STATUS` after the deletion says MESSAGES 1.
- MCP `wait_for_event` after_sequence pattern for bounded waits: keep the last `event.sequence`; `timed_out` true means nothing came.
- Cases of one program (C# and MCP recipes) repeat per claim with `stdout-matches` regexes; write them with json.dumps so YAML escapes survive (a regex dot is
  `\.` in a double-quoted YAML string, i.e. two backslashes in the file). To match a flag `Seen` preceded by a backslash use `.Seen`. A regex lookahead pair `^(?=.*a)(?=.*\nb)` checks two lines whose order is not fixed (two callbacks).
smtp-answer-try-later, smtp-resend-after-a-dropped-connection (both with twins), smtp-fail-delivery-for-one-recipient, smtp-collect-mail-with-an-event-callback, imap-list-mailboxes-and-counts-from-an-agent, pop3-retrieve-and-delete-from-an-agent (batch-08).

- `StoreChangedEvent.Account` is set only for the accounts store (Surl ADR-0171); find a delivery's owner through `DeliveredFor` or the `<owner>/INBOX/;UID=n` name.

## POP3 to IMAP on one mailbox (pop3-move-a-mail-client-to-imap + twins, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- `-v` log: each POP3 login is followed by `#N * Maildrop opened: <n> messages, <octets> octets` (count POP3 sessions with it); IMAP has no such line. List smtp first so verify's readiness probe is not a POP3 session, and wait for delivery over IMAP (`-X "FETCH 2 FLAGS" ... INBOX` with `--retry 10 --retry-all-errors --retry-delay 1`).
- POP3 RETR and IMAP `INBOX;UID=1` give the same bytes (cmp). UIDL `n <uidvalidity>.<uid>`. After a POP3 `DELE 1` the IMAP `INBOX?ALL` answers `* SEARCH 1` (sequence numbers renumber), so check with STATUS (MESSAGES 1) or UID 1 (exit 78).
- `--mailbox "ann@example.com:pw;protocols=smtp,imap"`: POP3 login 67 `Login denied` (Curl.Api `LoginDenied`, curl-mcp exitCode 67), IMAP fine, SMTP still delivers.
- Curl.Api: `new HttpMethod("UIDL")` on the POP3 root URL works (lists IDs); `new HttpMethod("DELE")` on `pop3://h:p/1` deletes when the client is disposed. `LoginJudgedEvent.Scheme` `pop3`/`imap`; MCP `get_events(kinds:["login_judged"], scheme:"pop3")`. Mail.DeliverAsync / seed_mail add no Received line, so sizes are fixed there; over SMTP the dated Received line can change them.

## Where did the mail go (imap-find-why-a-client-sees-no-mail, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- An accepted RCPT is answered `250 2.1.5 Recipient OK` (in get_exchanges, the sent chunk after `RCPT TO:<x>`), for a mailbox, a caught address and a discarded one alike. A discarded in-domain address leaves no store_changed event; with `--mail-catch-all` the caught copy is `delivered` to `<catchall>/INBOX/;UID=n`, its `To:` as the sender wrote it.
- MCP `get_events(kinds:["store_changed"], store:"mail")`: an IMAP fetch that sets \Seen is a `change` `written` event on the same `owner/INBOX/;UID=n` after the `delivered` one. `wait_for_event(kinds:["exchange_ended"], scheme:"smtp")` is a bounded wait for a curl-tool send to finish.

- `--allow-anonymous` mailbox owner (2026-11-22, Surl 5da5b3eaf): the empty name. Delivery event name `/INBOX/;UID=1`; Surl.Api `server.Mail.ListMessagesAsync("", "INBOX")` and `ReadMessageAsync("", "INBOX", uid)` (`SurlMailMessage.Bytes`) work; surl-mcp `list_messages`/`read_message` refuse `owner: ""` as invalid params (minLength 1), so an agent reads that mailbox over POP3 (`curl -s pop3://h:p/1`, `pop3://h:p/` lists). `curl -T alert.eml smtp://h:p/` sends `EHLO alert.eml` (file name appended to the URL) and the stored message starts `Return-Path: <from>` then `Received: from alert.eml ...` (mqtt-test-a-sensor-to-alert-flow and twins).

- surl-mcp rejects `owner: ""` for the anonymous mailbox (Surl BL-1593, open). Workaround: under `--allow-anonymous`, any non-empty owner name returns the anonymous mailboxes.

## Filing, folders and searching (imap-file-mail-into-a-folder, imap-rename-and-delete-a-folder, imap-search-by-sender-and-date + twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- Curl.Api 0.2.0 `CurlRequest.CustomRequest` (property, sent as written, wins over `Method`) sends any IMAP command with spaces: `new CurlRequest(HttpMethod.Get, imap + "INBOX") { Authentication = login, CustomRequest = "COPY 1 Archive" }`; STORE, COPY, MOVE (also `MOVE 1:2 Old`), EXPUNGE, CREATE, RENAME, DELETE, `UID SEARCH ...`, `STATUS INBOX (MESSAGES UIDNEXT)` (URL with no folder) all work; the body is the untagged answer (`* 1 FETCH (FLAGS (\Deleted))`, `* 1 EXPUNGE`, `* STATUS ...`). This supersedes "Curl.Api cannot STATUS/STORE/MOVE" above. A refusal is `CurlException` `Code` 21 `Quote command returned error`, no NO text. Curl.Api still CANNOT APPEND: Put + bytes or `FromStream(stream, length)` -> `CurlException` 25 `Cannot APPEND with unknown input file size`; file messages into a folder with `MOVE`/`COPY` from INBOX instead. `using Curl.Api.Errors;` for `CurlException`.
- Console: `-X "CREATE Archive" imap://h:p/` (URL ending `/`, prints nothing); `COPY`/`STORE`/`EXPUNGE`/`MOVE` need the folder in the URL (`.../INBOX`, curl SELECTs it) and take message NUMBERS. `STORE 1 +FLAGS (\Deleted)` prints `* 1 FETCH (FLAGS (\Deleted))`; `EXPUNGE` prints `* n EXPUNGE` per message on stdout; `MOVE` prints nothing (`-v`: `* OK [COPYUID ...] Moved`, `* 1 EXPUNGE`, `A004 OK MOVE completed`). COPY keeps the original and its flags (`Archive?FLAGGED` finds the copy); a \Deleted message still counts in STATUS and `?DELETED` names it until EXPUNGE. UIDs are never reused (UIDNEXT stays). `-X` chains with `--next` share one session.
- Refusals (all curl 21): CREATE existing `NO [ALREADYEXISTS] Mailbox already exists`; COPY to a missing folder `NO [TRYCREATE] Mailbox does not exist`; COPY/MOVE of a number the folder lacks `BAD Invalid message sequence number` (checked before the folder: an empty INBOX answers BAD even for a missing target); RENAME/DELETE of a missing folder `NO [NONEXISTENT] Mailbox does not exist`; RENAME onto an existing name (also INBOX) `NO [ALREADYEXISTS] Mailbox already exists`; `DELETE INBOX` `NO [CANNOT] INBOX cannot be deleted`.
- RENAME: `RENAME INBOX Mail2025` is ACCEPTED (RFC 3501 6.3.5): the messages move to `Mail2025`, an empty `INBOX` stays (UIDNEXT kept). `RENAME Old Archive` keeps the messages; `DELETE` of a folder with messages is silent and takes them. `RENAME Keep Work/Notes` lists `Work` as `\Noselect \HasChildren` and `Work/Notes`; the implied parent is not a folder (`DELETE Work`, `RENAME Projects Work` -> NONEXISTENT); `RENAME Projects/2025 Projects/2026` works. `curl -X 'RENAME Keep "Old Stuff"'` quotes a name with a space. LIST (`curl imap://h:p/` with no -X): INBOX first, then name order. A mailbox can be filled with `curl -T file imap://h:p/Old` (APPEND, stored before curl exits, CRLF fixtures).
- SEARCH over a URL query (`INBOX?FROM%20shop%20SENTSINCE%2001-Jun-2026`): FROM is a case-insensitive substring of the whole From header, display name included (`FROM "Shop Team"` -> that message); criteria combine (AND), `OR a b`; `SINCE`/`BEFORE` use the ARRIVAL date, `SENTSINCE`/`SENTBEFORE`/`SENTON` the `Date:` header (SENTSINCE inclusive); curl's APPEND and SMTP give no way to set an arrival date, so dated cases use SENT* plus SINCE 01-Jan-2020 (all) and 01-Jan-2099 (none). No match: `* SEARCH`, exit 0. Bad date (`SENTSINCE 2026-11-03`, `SINCE junk`): `BAD Invalid arguments`, exit 21. A search answers message numbers, which close up after an EXPUNGE; `-X "UID SEARCH FROM shop"` on `.../INBOX` answers UIDs; `;UID=3` takes a UID. `INBOX;UID=[3-4];SECTION=HEADER.FIELDS%20(FROM%20SUBJECT)` fetches two (glob). Surl.Api `DeliverAsync` keeps the `Date:` header as written.
- MCP: `curl` tool with `-v` gives the tagged reply in `standardError` (`< A004 BAD Invalid arguments`); the tag number is the command's place in the session (A003 for a URL with no folder, A004 after the SELECT). `list_mailboxes` names ordinal order (Archive, Drafts, INBOX, Keep, Mail2025, Old, Work/Notes); `list_messages` `messages[0].flags` holds `\Deleted`. curl-mcp `-T first.eml imap://.../Old` reads recipe fixtures from CURL_MCP_ROOT.
imap-file-mail-into-a-folder, imap-rename-and-delete-a-folder, imap-search-by-sender-and-date (all with twins).

## Quotas, flags, UIDs, refusals in the log (batch-11, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--mailbox "ann@example.com:pw;quota=1k"`: cap on the bytes ALL the owner's mailboxes hold, as stored (Return-Path + dated Received
  added over SMTP: a 324-byte LF file sent with --crlf is stored as 460; `FETCH 1 RFC822.SIZE`). `1k` = 1024; `-v` start line `* Mailbox
  ann@example.com: logins on smtp, imap, pop3, quota 1024 bytes`. `quota=0` -> exit 2 `surl: option --mailbox: key quota needs a value above 0`
  (no `(2)` prefix); `quota=lots` -> exit 2 `expected a proper numerical parameter`. Surl.Api `Mailboxes.Add("...;quota=1k")`; refusal
  `SurlServerStartException` FailedInit, `Lines[0]` `option --mailbox: key quota needs a value above 0`; MCP start_server tool error exit_code 2.
- SMTP over quota: RCPT 250, DATA 354, then after the final `.` `452 4.2.2 Mailbox full`; curl exit 8 `Weird server reply` (`-w %{response_code}`
  452); Curl.Api `CurlException` `WeirdServerReply` (8). Log: `#N * Message refused: a recipient's mailbox would pass its quota`. One full
  mailbox among several recipients refuses the WHOLE message (the other recipient gets nothing). A mailbox without quota= is unaffected.
- IMAP APPEND over quota: `A003 NO [OVERQUOTA] Mailbox is over its quota`, curl exit 25; log `APPEND refused: 663 bytes would pass the
  mailbox's quota`. `Mail.DeliverAsync` -> `SurlSeedRefusedException` `Refusal` QuotaExceeded; MCP seed_mail tool error `Mail: Mail for
  ann@example.com was refused: QuotaExceeded.` A \Deleted message still counts; EXPUNGE frees the room. CAPABILITY has no QUOTA extension.
- Curl.Api at Curl c84035f797 HAS `CurlRequest.CustomRequest` (any IMAP command: `STORE 1 +FLAGS (\Deleted)`, `EXPUNGE`, `UID SEARCH UID 3:*`,
  `UID FETCH 1:* FLAGS`, `STATUS INBOX (UIDNEXT UIDVALIDITY)` on the root URL), with `HttpMethod.Get`; StatusCode 0, Body is the untagged answer.
  The lines above saying Curl.Api cannot STORE/STATUS are superseded. IMAP APPEND still fails: byte body or `CurlContent.FromStream(s, length)`
  -> CurlException 25 `Cannot APPEND with unknown input file size`.
- FETCH FLAGS: `-X "FETCH 1:* FLAGS"` on .../INBOX -> one line per message, `* 3 FETCH (FLAGS (\Deleted \Seen))`, `()` for none; sets no flag.
  Surl.Api `SurlMessageSummary.Flags` / MCP list_messages `flags` order them `\Seen \Deleted` (the other way round). `INBOX?DELETED`,
  `INBOX?SEEN%20NOT%20DELETED` search keys work. `-X EXPUNGE` -> `* 3 EXPUNGE`; after it `UID FETCH 1:* FLAGS` -> `* 3 FETCH (UID 4 FLAGS ())`.
- UIDs: `-X "UID SEARCH UID 3:*"` answers UIDs; curl's URL query `INBOX?UID%203:*` sends plain `SEARCH UID 3:*` (`-v` `> A004 SEARCH UID 3:*`)
  and answers MESSAGE NUMBERS (they differ after an EXPUNGE). `UID SEARCH UID 5:*` with largest UID 4 answers `* SEARCH 4` (RFC: `*` is the
  largest). UIDs are not reused after an expunge (next is UIDNEXT). A fetch of an expunged UID: curl 78, Curl.Api RemoteFileNotFound.
  STATUS UIDVALIDITY equals Surl.Api `SurlMailboxInfo.UidValidity` / MCP list_mailboxes `uid_validity`. curl sends `UID FETCH n BODY[]`.
- `-v --log-file mail.log` SMTP refusals: the event line just before each reply gives the reason: `* RCPT x refused: outside every
  --mail-domain` (550 5.7.1), `* MAIL refused: log in with AUTH first, or give --allow-anonymous` (530 5.7.0), `* Login refused: CRAM-MD5 app@x`
  after `< ****` (535 5.7.8; curl 67, Curl.Api LoginDenied), `* Message refused: ...quota` after `< .` (452). The exchange's last line is
  `#N * Exchange N ended; closing the connection.`; a script waits for it (smtp-see-why-mail-was-refused/why-refused.sh). Surl.Api exchange
  chunks are one line each; the record keeps the CRAM-MD5 answer as sent. Curl.Api with no Authentication: `SendError` (55) `MAIL failed: 530`.
  curl CLI sends `MAIL FROM:<a> SIZE=<file bytes>`; Curl.Api sends no SIZE.
imap-check-the-flags-an-app-left, imap-enforce-a-mailbox-quota, imap-fetch-only-new-messages, smtp-see-why-mail-was-refused (all with twins).

- IMAP search by UID: curl sends a URL query (`?UID 3:*`) as `SEARCH <query>`, so the answer is message numbers; for UIDs use `-X "UID SEARCH UID 3:*"` (Curl session, 2026-11-22; BL-5757 confirms against upstream 8.22.0). An SMTP 452 after DATA exits 8, likely upstream's own answer (BL-5756 measures it).

## Draining a maildrop (pop3-drain-a-maildrop and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- Console drain in one command: `curl -sS -u a:pw -o "msg#1.eml" "pop3://h:p/[1-3]" --next -sS -u a:pw -X DELE "pop3://h:p/[1-3]"` (curl range: three RETRs into msg1..3.eml, then `DELE 1`, `DELE 2`, `DELE 3`, one session, commit at QUIT when curl exits, exit 0). A new session's LIST prints a lone CRLF; IMAP STATUS says MESSAGES 0. The same range on an empty maildrop: `RETR 1` -ERR, exit 8. `-X DELE .../[1-3] --next -X RSET` deletes nothing (exit 0, still 3). `-o "nosuch/msg#1.eml"`: curl exit 2 `Could not create the output file nosuch/msg1.eml` before any DELE (with or without `--fail-early`).
- Curl.Api: one `CurlClient` = one session; GET root = LIST; `new HttpMethod("RETR")`/`("DELE")` + path `n`; `new HttpMethod("RSET")` + empty path works. Dispose the client (QUIT), then `server.WaitForEventAsync(ExchangeEnded, AfterSequence = last, 10 s)` (needs `RecordExchanges = true`): after it the store reads empty at once, no polling.
- curl-mcp: `-o dir/msg#1.eml` with a missing `dir` is NOT curl exit 2: the call is a tool error with the plain text `An error occurred invoking 'curl'.` (not JSON; test `text.StartsWith('{')` before parsing), and no connection is made. An existing folder under `CURL_MCP_ROOT` works (`#1` expands). Every curl-mcp run is one POP3 connection: `wait_for_event(kinds:["exchange_ended"], after_sequence: last)` after each call takes the next one in order; `list_messages` afterwards shows the deletions. `curl -v ... pop3://h:p/1` on an empty maildrop: exitCode 8, stderr has `< -ERR No such message`.

## Waiting on a fake clock (smtp-wait-for-mail-on-a-fake-clock; Surl 5da5b3eaf, 2026-11-22, windows)
- `SurlServerOptions.TimeProvider` takes a `System.TimeProvider`: `WaitForEventAsync(query, timeout, ct)` (needs `RecordExchanges = true`, else `InvalidOperationException`) counts its timeout on it. A hand-written `FakeClock : TimeProvider` (`GetUtcNow`, `GetTimestamp`, `TimestampFrequency`, `CreateTimer` returning an `ITimer` that fires when `Advance(by)` passes its due time) works: still waiting after 29 s of fake time, `TimeoutException` after 30 (`No event matching SurlEventQuery {...} within 00:00:30`), about 0.3 s of real time. A real `CancellationToken` guard still applies. `SmtpClient` with credentials needs `AllowPlaintextAuthentication` (else `SmtpException` `MustIssueStartTlsFirst`); delivered mail is a `StoreChanged` event, `Change` `Delivered`, `Store` `Mail`, `Name` `ann@example.com`.

## Delivered-To, foreign domains, mailbox files, IMAP-only POP3 (batch-12, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--mail-delivered-to` (Surl.Api `MailDeliveredTo`, MCP in `arguments`): each owner's copy starts `Delivered-To: <envelope rcpt>` ahead of Return-Path,
  so a test reads the RCPT address from the store; a message whose `To:` says dave but whose RCPT is bob is in bob's mailbox with `Delivered-To: bob@...`.
  `HEADER.FIELDS (DELIVERED-TO TO)` fetches it. IMAP SEARCH `TO x SUBJECT "..." BODY https://host/path/` (unencoded `:` and `/` in the URL query work).
- Bounded "never arrives" wait: console `--retry 2 --retry-all-errors --retry-delay 1` on `INBOX;UID=1;...` -> exit 78 after ~2.5 s; Surl.Api
  `WaitForEventAsync(StoreChanged, Name = owner, 2 s)` throws `TimeoutException` (needs `RecordExchanges = true`); MCP `wait_for_event` -> `timed_out`.
- `--mail-domain` match (two domains served): `ann@mail.example.com` (subdomain) and `ann@example.com.evil.org` refused 550; `ANN@EXAMPLE.COM`
  and `ann@example.com.` (trailing dot) accepted and delivered to ann; `ann@example.co` refused. Envelope sender outside every domain accepted; a
  `Cc:` header naming an outsider with an in-domain RCPT accepted (only RCPT is judged). `-v` log per refusal `#N * RCPT x refused: outside every
  --mail-domain` then `#N > 550 5.7.1 Relaying denied\r\n` (the reply logged escaped); `-X "LIST 1" pop3://...` prints nothing (exit 0) once message 1 exists.
- `--mailbox-file f.csv` (Surl.Api `MailboxFiles`): `-v` start lines `* Mailbox x: logins on ...` per record (MCP `log_lines` too). Columns in any order
  and case, values follow the header order. Refusals, exit 2 `surl: (2) Mailbox file f.csv, line N: <reason>`, N the PHYSICAL line (blank lines and
  quoted multi-line fields count): `not a mail address`, `mailbox x is not in any --mail-domain`, `mailbox x is given twice` (also across two files
  and against a `--mailbox`), `unknown column pw` (line 1), `expected 2 fields` (too few or too many), `a quoted field has no closing quote`,
  `an empty field` (empty password), `unknown scheme nosuch`, `mailbox x needs --mail-domain`. BUT a scheme with no login (`gopher`) is refused
  without file or line: `surl: (2) scheme gopher has no login`. Missing file exit 37 `surl: (37) Could not read mailbox file f.csv` (Surl.Api
  `CouldNotReadFile`). `"s3cret,too"` quoted is the password `s3cret,too`. A UTF-8 BOM and CRLF are fine. Surl.Api `Lines[0]` names the file
  as given (full path if given so). surl-mcp reads the path relative to SURL_MCP_ROOT; `../x.csv` -> tool error `{"refused":"--mailbox-file names
  ../x.csv, which is not inside the root"}`; a refusal from surl -> `exit_code` 2 / 37 with `lines`.
- `;protocols=imap` mailbox over POP3: CAPA still offers `SASL CRAM-MD5`; right password -> `-ERR [AUTH] Authentication failed`, curl 67, the
  same reply as an unknown address or a wrong password; log `* Login refused: CRAM-MD5 <account>` alike for all three (no reason). Surl.Api
  `LoginJudgedEvent` (Scheme pop3, Mechanism CRAM-MD5, IsAccepted false); MCP `get_events login_judged` also has `is_refused_by_fault`.
  curl `-v` shows `< -ERR [AUTH] Authentication failed` (with `-s`; without it the progress meter shares the line). Curl.Api: `using Curl.Api.Codes;`
  for `CurlCode.LoginDenied` in a `when` filter. JsonElement in an interpolated string prints `False`; use `GetRawText()` for JSON `false`.
smtp-check-an-app-sent-the-right-mail, smtp-fail-a-test-on-mail-to-another-domain, smtp-load-mailboxes-from-a-csv-file, pop3-limit-a-mailbox-to-imap (all with twins).
- Re-pin 3 (Surl d64faa4bb): surl-mcp `list_messages`/`read_message` take `owner: ""`, the `--allow-anonymous` mailbox owner (supersedes the earlier lines saying they refuse it, Surl BL-1593). `list_messages` answers `messages[0].uid` 1 after one delivery.
- Re-pin 3 (Curl 2145a49eae, BL-5753): a Curl.Api `PUT` with `CurlContent.FromStream(stream, length)` to `imap://h:p/Shared/team` APPENDs (empty body, no exception); was curl 25 `Cannot APPEND with unknown input file size`.

## Traces, refused recipients, -F attachments, two domains (batch-13, Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-22, windows)
- `surl --trace-ascii f` over SMTP: `#N * Exchange N opened: smtp on smtp://h:p/ from ...`, then blocks `#N <= Recv data, n bytes` (client to server) /
  `#N => Send data, ...` (server to client), each followed by `0000: <line>` data lines (a DATA block holds the whole message, one data line per
  message line, offsets in hex). The CRAM-MD5 answer is `0000: ****`; `Message stored: N bytes for 1 recipients` sits before the 250; the exchange
  ends `#N * Exchange N ended; closing the connection.`. A reading script (`dialogue.sh` in smtp-see-exactly-what-an-app-sent) waits for `ended` and turns
  the blocks into `C:`/`S:` lines; `tr -d '\r'` first. The trace is the wire, not the store: the stored copy adds Return-Path and Received.
- Surl.Api `SurlExchangeRecord.Chunks` and MCP `get_exchanges` keep the CRAM-MD5 answer raw (base64 of `user hexdigest`), while `-v`/trace/`Log` write
  `****`. A Curl.Api DATA chunk holds every message line in one chunk; split on CRLF. Read the record after `ExchangeEnded` (dispose the CurlClient
  first: it holds the connection, QUIT comes at dispose).
- Recipient replies: `RCPT TO:<x>` -> `250 2.1.5 Recipient OK` or `550 5.7.1 Relaying denied`; `MAIL FROM` -> `250 2.1.0 Sender OK`; DATA -> `354 End data
  with <CR><LF>.<CR><LF>`, then `250 2.0.0 Message accepted`; QUIT -> `221 2.0.0 Bye`. curl CLI's MAIL FROM has `SIZE=<file bytes>` (117 for a 117-byte CRLF
  file, 112 for a 108-byte LF file sent with --crlf); Curl.Api sends SIZE for a CurlMultipartContent body, none for a byte body.
- `--mail-rcpt-allowfails`, one of four refused: exit 0, `-sS` prints NOTHING (stdout and stderr empty), `-w %{response_code}` 250; `curl -v 2>&1 | grep -E
  "^> RCPT|^< 250 2.1.5|^< 550" | tr -d "\r"` lists each RCPT with its reply. All recipients refused even with allowfails: exit 55 `curl: (55) RCPT failed:
  550 (last error)`. Curl.Api has no per-recipient answer (no debug callback in 0.2.0): with `MailRecipientAllowFails` the response is 250 and names
  nobody; one request per recipient gives each its own `CurlException` (`SendError`, `RCPT failed: 550`). A CurlClient reused after a refused RCPT carries on.
- `curl -F` over SMTP (no field name: `-F "=text"`, `-F "=@report.csv;type=text/csv;encoder=base64"`, headers by `-H "Subject: ..."`): curl writes
  `Content-Type: multipart/mixed; boundary=------------------------<22 random>`, `Mime-Version: 1.0`, then the -H lines; the text part has NO header
  (BODYSTRUCTURE `("TEXT" "PLAIN" ("CHARSET" "US-ASCII") NIL NIL "7BIT" 37 1 ...)`); the file part `Content-Disposition: attachment; filename="report.csv"`,
  `Content-Type: text/csv`, `Content-Transfer-Encoding: base64`. Without type=/encoder=: `application/octet-stream`, `8bit`, bytes raw (bare LFs kept).
  Missing file: exit 26 before connecting. Curl.Api: `Body = new CurlMultipartContent([CurlMimePart.Text(null, "..."), CurlMimePart.Bytes(null, bytes) with
  { FileName = "report.csv", ContentType = "text/csv", Encoder = "base64" }])`, `Headers = [new("Subject", "...")]`, POST: the same message. A Bytes part with
  no ContentType/Encoder has ONLY Content-Disposition (no Content-Type, no encoding). `CurlMimePart.File(null, "nosuch.csv")`: CurlException 26 `ReadError`.
  curl-mcp reads `-F @file` from CURL_MCP_ROOT.
- Two domains (`--mail-domain one.test --mail-domain two.test`, `--mailbox ann@one.test:..`, `--mailbox ann@two.test:..`): separate mailboxes and accounts;
  ann@two.test with ann@one.test's password and bare `ann` both curl 67 (Curl.Api LoginDenied). One message to bo@one.test and bo@two.test: a copy each.
  ann@three.test and ann@mail.one.test: 550. cy@two.test (no mailbox): 250, log `* Mail for cy@two.test discarded: no such account` then `Message stored: ...
  for 0 recipients`; no store_changed event; MCP `list_mailboxes cy@two.test` tool error `No mailboxes for cy@two.test.`.
- Two senders with different logins: smtp-serve-two-mail-domains-in-csharp gives each its own CurlClient, as a precaution (a reused connection keeps its login for IMAP; not probed for SMTP).
smtp-see-exactly-what-an-app-sent, smtp-send-despite-a-bad-recipient, smtp-send-mail-with-an-attachment, smtp-serve-two-mail-domains (all with twins).

## Size limit and mail that never arrives (smtp-explain-why-mail-never-arrived, Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-22, windows)
- `--max-filesize 2k` applies to SMTP: EHLO offers `250-SIZE 2048`; curl declares `MAIL FROM:<a> SIZE=<file bytes>` and a message over the limit is refused there, `552 5.3.4 Message size exceeds the size limit`, before DATA; curl exit 55 `MAIL failed: 552` (same with --mail-rcpt-allowfails). Nothing stored, no event.
- `--mail-rcpt-allowfails` with one recipient outside the domain: exit 0, `RCPT TO:<bob@example.org>` 550 5.7.1 in the record, the other delivered. An in-domain address with no mailbox: 250 throughout, no store_changed within a 5 s wait_for_event (timed_out), `list_mailboxes` tool error `No mailboxes for carol@example.com.`.

## Locks, paging, seed refusals, threads (batch-14, Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-24, windows)
- POP3 maildrop lock: while one session is logged in (until QUIT), a second login with the right password gets `-ERR [IN-USE] Maildrop is locked by another session`, curl exit 67 `Login denied`; log `* Login accepted: CRAM-MD5 x` then `* Maildrop locked by another session`. Hold the first open with `curl --limit-rate 100 pop3://h:p/1` on a ~1 KB message (pop3-refuse-a-second-session/two-clients.sh waits on the `Maildrop opened` count).
- Surl.Api `ListMessagesAsync(owner, mailbox, afterUid, limit, ct)`: HasMore is exact (a full last page says false); limit 0 or 1001 -> `SurlReadRefusedException` `The limit must be 1 to 1000.`; unknown owner/folder `SurlNotFoundException` `No mailbox INBOX for x.`. MCP `list_messages` limit 0/1001 -> McpProtocolException `Invalid params: arguments.limit: expected at least 1` / `at most 1000`; after_uid past the end -> `{"messages":[],"has_more":false}`.
- `DeliverAsync` / seed_mail refusals: in-domain unknown `NoSuchAccount`, outside `RelayingDenied`, `ann`/`ann@`/empty `InvalidAddress`; `ANN@EXAMPLE.COM` delivered. seed_mail refusal is a tool error with text only (no structured content). With `MailCatchAlls` the unknown address is delivered to the catch-all, no exception.
- `SurlEventQuery { Kind = StoreChanged, Store = Mail, Scheme = "smtp" }` leaves out Seeded (DeliverAsync) events. OnEvent sees only events after registration. One send to two mailboxes: two events, same ExchangeId. MCP seed events carry `delivered_for`; SMTP delivery events carry `exchange_id`.
- MCP `wait_for_event` without `after_sequence` answers an already kept event at once (e.g. an earlier seed): take the max `sequence` from `get_events` before sending.
- IMAP has no THREAD/SORT (`THREAD REFERENCES UTF-8 ALL`, `SORT` -> curl 21). `UID FETCH 1:* ENVELOPE` gives one line per message ending `<in-reply-to or NIL> "<message-id>"`; `UID SEARCH HEADER References "<id>"` works; `FETCH 1:* (UID BODY.PEEK[HEADER.FIELDS (...)])` via -X prints only the first message's literal. SUBJECT search is a case-insensitive substring; a quoted phrase is one string.
imap-page-through-a-large-mailbox-in-process, imap-page-through-messages-from-an-agent, imap-search-a-mailbox-from-an-agent, imap-seed-a-thread-of-replies-from-an-agent, pop3-refuse-a-second-session, smtp-catch-a-refused-seed (+ -from-an-agent), smtp-collect-delivered-mail-with-a-callback, smtp-confirm-a-delivery-without-polling-from-an-agent, smtp-serve-a-mail-domain-from-an-agent.
