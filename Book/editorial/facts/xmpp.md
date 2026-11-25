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

## Requiring TLS before login (xmpp-require-tls-before-login and twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- CORRECTION: `xmpp` with no `--cert`, no `--self-signed` and no `--allow-plaintext-auth` no longer starts: `surl: (58) xmpp://127.0.0.1:P/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one`, exit 58 (Surl.Api `SurlServerStartException` `ExitCode` 58; surl-mcp tool error `exit_code` 58 `CertificateProblem`). `--allow-plaintext-auth` alone starts it, offering `<mechanisms>` SCRAM-SHA-256, SCRAM-SHA-1, PLAIN at once, no STARTTLS; curl logs in there with no TLS.
- Raw client: `curl -m 3 -T open.xml telnet://127.0.0.1:P` (fixture holds the stream header) prints the server's header and features and exits 28 after `-m`. With a cert: `<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'><required/></starttls></stream:features>`, no mechanisms. Header plus `<auth ... mechanism='PLAIN'>AGFsaWNlAHMzY3JldA==</auth>` (alice/s3cret) before TLS: `<failure xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><encryption-required/></failure>`. The stream `id` is random hex. On the plaintext server the same PLAIN is `<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>`. Not probed on Linux/macOS (CI will say).
- By hand in C#: send `<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>`, read `<proceed .../>`, wrap the stream in `SslStream` (`X509Certificate2.CreateFromPemFile("server.pem", "server.key")` for the hash; the one-argument overload throws), send the header again, mechanisms (PLAIN among them) are offered, PLAIN login gives `<success`.
- curl over TLS with `-k` still logs in; no `-u`: exit 67 `XMPP server does not offer SASL ANONYMOUS`; wrong password 67 `XMPP authentication failed: not-authorized`; no `--cacert` exit 60. Curl.Api: `CurlException` 67 / 60 with the same messages; xmpps works with the same `CertificateAuthorityFile`. No `--ssl-no-revoke` needed here.
- surl-mcp `get_exchanges` after a STARTTLS login: chunks in order, decrypted: features (starttls `<required/>`), received `<starttls`, `<proceed`, new header, `<mechanisms`, received `<auth` (SCRAM-SHA-256), `<success`. For a raw early login the header and `<auth` arrive as one received chunk, then features, then `<failure><encryption-required/>`.

## Login events (xmpp-watch-a-login-from-an-agent; Surl v0.1.8 9f8005dff / Curl 2145a49eae, 2026-11-25, windows)
- surl-mcp `get_events(kinds:["login_judged"])` after `wait_for_event(exchange_ended)`: `{scheme:"xmpp", account:"alice", mechanism:"SCRAM-SHA-256", is_accepted, is_refused_by_fault}`. curl over STARTTLS uses SCRAM-SHA-256 (wrong password: event refused, curl 67 `not-authorized`). `--auth plain` (warns `--auth: accepted methods are plain`): curl logs in with PLAIN. `add_fault refuse-login`: same curl error, event `is_refused_by_fault: true`. curl `--login-options AUTH=PLAIN` does NOT change the XMPP mechanism. No `-u` (67 `does not offer SASL ANONYMOUS`), untrusted cert (60), and a raw PLAIN before STARTTLS (`<encryption-required/>`): no login_judged event at all.
