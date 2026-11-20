<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# xmpp facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-20, windows.

## Listen URLs and readiness
`xmpp` (STARTTLS) and `xmpps` (TLS from the first byte); TCP, `ready: port`. The listen host is the server's domain; `--mail-domain chat.example` adds a second name, and the server answers under both. `xmpp` without `--cert`/`--self-signed` and without a loosening option starts anyway and offers empty stream features (xmpp-send-a-message review).

## Server options that matter
- `--cert server.pem --key server.key`: `xmpp` then offers STARTTLS, required before a login. Made with `setup:` openssl.
- `--user alice:s3cret`: an account. Mechanisms: SCRAM-SHA-256, SCRAM-SHA-1, PLAIN, only under TLS (curl logs in with SCRAM-SHA-256).
- `--allow-plaintext-auth` (loosens, warns): no TLS needed, PLAIN over the clear stream.
- `--allow-anonymous` (loosens, warns): curl without `-u` still fails, exit 67 `invalid-mechanism` (no `ANONYMOUS` is offered).

## Secure by default
- No `-u`: exit 67, `XMPP server does not offer SASL ANONYMOUS`.
- Wrong password: exit 67, `XMPP authentication failed: not-authorized`.
- Untrusted certificate (no `--cacert`): exit 60 before the login. The text is the TLS library's (schannel on Windows): match the exit code.

## Client: curl
- `xmpp://host:port/<jid-or-domain>`; with `-d`, a message; with no `-d`, disco info of the path. `?disco` is the same; `?disco;items`, `?disco;info` and `?disco;items=1` all still print the info answer; `?disco;node=x` is `item-not-found`; any other query type is exit 3 `Unsupported XMPP query type: items`.
- Disco info of the domain: `<query xmlns="http://jabber.org/protocol/disco#info"><identity category="server" type="im" /><feature var=".../disco#info" /><feature var=".../disco#items" /><feature var="urn:xmpp:ping" /></query>` and a newline. Of an account bare JID (`alice@127.0.0.1`, the logged-in one): identity `account`/`registered`, the two discovery features, no ping.
- Stanza errors, curl exit 8 `XMPP stanza error: <name>`: `service-unavailable` (account with no stream bound, e.g. `bob@...`), `remote-server-not-found` (other domain), `item-not-found` (unknown node).
- curl cannot ask `disco#items`. A raw client (login PLAIN, bind, IQ get) is answered `<query xmlns='.../disco#items'/>`, empty, for the domain and for the account.

## MCP
`start_server(listen_urls, arguments=["--cert","server.pem",...])`. curl-mcp `curl` tool as for any scheme (`exitCode`, `standardOutput`/`standardError`). `get_exchanges` returns the decrypted XMPP text of each exchange: curl's disco question is `<iq type="get" id="disco" to="127.0.0.1" xmlns="jabber:client"><query xmlns="http://jabber.org/protocol/disco#info" /></iq>`.

## C#
- Surl.Api: `SurlServerOptions.CertificateFile/KeyFile/Accounts/AllowPlaintextAuthentication/MailDomains`.
- Curl.Api 0.2.0: `GET xmpp://.../<path>` with `CurlAuthenticationOptions` user/password and `CurlTlsPeerOptions.CertificateAuthorityFile` prints the answer. Stanza error: `CurlException` 8 `WeirdServerReply`; login: 67 `LoginDenied`.

## Gotchas
- `--allow-anonymous` does not make curl log in (see above); use accounts.

## Recipes
xmpp-ask-what-a-server-offers (and twins); also xmpp-send-a-message.
