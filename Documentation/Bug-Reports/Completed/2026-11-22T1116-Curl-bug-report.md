<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (batch 11 of the 1,000-recipe plan)

From writing and verifying batch 11 (62 recipes: IMAP, MCP, WebTransport, CoAP over DTLS, DNS zones, STOMP, AMQP over TLS, CI scenarios, object stores) of the *Surly* cookbook against Curl `c84035f797`, Surl `5da5b3eaf`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api IMAP (BL-5610) | APPEND still fails: a `PUT` with bytes, or `CurlContent.FromStream(stream, length)`, throws `CurlException` 25 `Cannot APPEND with unknown input file size`, though BL-5610's fix is in this build. Two writers hit it independently |
| 2 | Medium | Curl.Api MCP | A body sent to an `mcp://`, `mcps://` or `mcp+stdio:` URL answers the tool list; expected a `tools/call` with that body |
| 3 | Medium | Curl.Api WebTransport | A body sent to a `webtransport://` URL is not sent; the session opens, the login works, the response body is empty. Expected the data sent and echoed, as `curl -d` does |
| 4 | Low | SMTP over quota | A send to a full mailbox, refused with `452` after DATA, exits 8 "Weird server reply"; 55 (or another send error) was expected |
| 5 | Low | IMAP URL | `imap://h:p/INBOX?UID%203:*` sends a plain `SEARCH`, so the server answers message numbers, not UIDs; RFC 5092's search part with `UID` expected `UID SEARCH` |

## Repros

**1.** `surl --mailbox ann@example.com:pw imap://127.0.0.1:1143/`; Curl.Api `new CurlRequest(HttpMethod.Put, "imap://127.0.0.1:1143/INBOX") { Body = CurlContent.FromStream(new MemoryStream(eml), eml.Length), Authentication = ... }` → `CurlException` 25. Recipes `imap-enforce-a-mailbox-quota-in-csharp`, `imap-rename-and-delete-a-folder-in-csharp` use other routes.

**2.** `surl mcp://127.0.0.1:8765/`; Curl.Api `POST` to `mcp://127.0.0.1:8765/` with a `tools/call` JSON body → the tools list comes back. Recipe `mcp-start-an-mcp-server-as-a-program-in-csharp`.

**3.** `surl --cert server.pem --key server.key --user a:b webtransport://127.0.0.1:4433/`; Curl.Api `POST` with a 100-byte body → status 200, empty body; surl records no data. `curl -d @file webtransport://...` echoes it. Recipe `webtransport-send-datagrams-and-streams-in-csharp` (retitled "Opening a WebTransport Session in C#" for now).

**4.** `surl --mail-domain example.com --mailbox "ann@example.com:annpass;quota=1k" smtp://127.0.0.1:2525/` (as in `imap-enforce-a-mailbox-quota`), fill the mailbox, then `curl --mail-rcpt ann@example.com -T big.eml smtp://...` → exit 8.

**5.** `curl -u ann@example.com:pw "imap://127.0.0.1:1143/INBOX?UID%203:*"`; surl's log shows `SEARCH UID 3:*`... answered with sequence numbers.
