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
- `--allow-plaintext-auth` (loosens, warns): also offer PLAIN/LOGIN without TLS. Needed by .NET SmtpClient.
- Envelope sender is NOT checked against the login: a logged-in app may `MAIL FROM` any address, even
  outside the domain.

## Secure by default
- No login: SMTP `MAIL` -> 530 (curl 55, `MAIL failed: 530`; SmtpClient `5.7.0 Authentication required`);
  IMAP `NO [AUTHENTICATIONFAILED]` (curl 21); POP3 `-ERR [AUTH]` (curl 8).
- Without TLS only `CRAM-MD5` is offered (SMTP `250 AUTH CRAM-MD5`, POP3 `SASL CRAM-MD5`, IMAP
  `LOGINDISABLED AUTH=CRAM-MD5`). curl and Curl.Api use it; no loosening needed. Wrong password: curl 67.

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
  (substring match); `INBOX?ALL`. Missing UID: exit 78.
- POP3: `pop3://h:p/` LIST `1 <octets>` per line (empty maildrop prints a lone CRLF); `/1` RETR;
  `-X "DELE n"`, `-X UIDL`, `-X RSET`; `-X STAT` prints nothing, `-v` shows `< +OK <count> <octets>`.
  `--next` POP3 transfers share one session; DELEs commit at QUIT, and a failing DELE later in the chain
  (exit 8) does not undo earlier ones. Wrong password with n `--next` transfers: n `Login denied` lines.

## .NET
- Curl.Api 0.2.0 CAN send SMTP, but only with a stream body: `new CurlRequest(HttpMethod.Put, smtpUrl + "example.com")`
  with `Mail = new CurlMailOptions { MailFrom = .., MailRecipients = [..] }`, `Authentication`, and
  `Body = CurlContent.FromStream(new MemoryStream(bytes))` -> `StatusCode` 250 (CRAM-MD5, no loosening).
  A byte[] body is POSTFIELDS, not an upload: GET/PUT with byte[] -> 500 (CurlException 8), Post does VRFY (252).
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
- curl-mcp `curl(args)`: `exitCode`, `standardOutput.content`, `standardError.content`; non-zero exit is
  a tool error (`isError`). `-T file` reads from CURL_MCP_ROOT.

## Gotchas
- Fixture line ends: LF fixtures need `--crlf`; CRLF fixtures in mcp recipes avoid it but it is harmless.
- `Received:` carries the client's EHLO name (SmtpClient sends the machine name): never match it exactly.
- `curl -T "order[01-10].eml"` (a glob) sends ten messages in one SMTP session; good for `seed:`.
- curl prints a progress meter on stderr for multi-transfer IMAP/POP3 commands without `-s`.

## Recipes
smtp-send-and-read-it-over-imap, pop3-retrieve-and-delete-a-message, smtp-serve-a-mail-domain,
smtp-check-the-mail-an-app-sent (+ -in-csharp, -from-an-agent), smtp-catch-every-address-in-one-mailbox
(+ twins), smtp-fail-a-test-on-a-wrong-recipient (+ twins), pop3-empty-mailboxes-after-a-run,
surl-seed-a-mailbox-before-a-test (+ twins), surl-hand-bound-ports-to-the-app (+ twins).
