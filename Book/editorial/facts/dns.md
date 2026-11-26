<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# dns facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-20, windows.

## Listen URLs and readiness
`dns://host:port/` serves UDP and TCP on the one port (`--dns-transport` picks others: tls, quic, https).
`ready: port` works, because the TCP listener is up; verify's port is free for UDP too. Port 5353 is
refused on Windows (exit 45, Permission denied); use `{{port}}`.

## Server options that matter
- `--directory <dir>`: zones are master files `<dir>/.surl/dns/<apex>.zone`, read once at start. A fixture
  `site/.surl/dns/example.test.zone` works (the `.test` apex is fine). `$ORIGIN`, `$TTL`, relative names work.
- No `--directory` or no zone file: every query is REFUSED. A name in no zone is REFUSED.
- `--allow-zone-transfer`: loosens (no stderr warning seen); AXFR over TCP sends the zone, the apex SOA first and last (`dns-transfer-a-whole-zone`). Default: REFUSED over TCP and UDP. With it: UDP AXFR is NOTIMP; AXFR of a name that is not an apex (even one inside the zone) is NOTAUTH, which curl writes `RCODE9`.

## Client: curl
curl 8.22.0 has no `dns` scheme; the Curl port adds `dns://server:port/name?type=MX` (RFC 4501; `;TYPE=MX`
in the path does not work, use `?type=`). Type defaults to A.
- Default output is dig-like text: `;; ->>HEADER<<- opcode: QUERY, status: NOERROR, id: N`, a flags line, then
  sections, tab-separated lines (`example.test.<TAB>300<TAB>IN<TAB>MX<TAB>10 mail.example.test.`).
- `--dns-json`: one line of JSON plus a line feed: `{"id":N,"opcode","status","flags":[...],"question":[...],
  "answer":[...],"authority":[...],"additional":[...]}`; a record is `{name,type,class,ttl,data}`; data fields:
  MX `preference` (number) and `exchange`; TXT `txt` (array of strings); A/AAAA `address`; SOA `mname rname
  serial refresh retry expire minimum`; NS `nsdname`. `id` is random: match with a pattern.
- `--dns-transport tcp|udp`. Any status, REFUSED and NXDOMAIN included, is exit 0.
- A name with a type it has no record of: NOERROR, empty answer, the SOA in authority.
- MX answers also fill `additional` with the glue A record when the zone has it; the authority holds the NS.

- curl asks `type=AXFR` only: `type=IXFR` is curl exit 3 `The dns: URL's query element "type=IXFR" is not TYPE= or CLASS= with a known value`, before any query (Curl.Api: `CurlException` code 3). AXFR over UDP and TCP both exit 0.
- Two surls cannot share one `--directory` (lock): a second server in a recipe needs its own copy of the zone fixture.

## MCP
- surl-mcp `add_dns_zone`: `server_id`, `apex`, `zone_text`; answers `{"apex","byte_count"}`. A second zone for an
  apex: tool error `Zones: The zone example.test was refused: a second zone for example.test.`; on a server with no
  DNS listener: `... This instance holds no DNS zones.`
- surl-mcp `read_zone`: `server_id`, optional `apex`; no apex answers `{"zones":["example.test."]}`; with one
  `{"apex","zone"}` (master text, absolute names, TTL and class on every line; spaces inside a TXT string are
  written as backslash 032). Unknown apex: tool error `No such zone.`
- curl-mcp `curl` with `-s --dns-json <url>` answers JSON text whose `standardOutput.content` is itself the dns JSON.
  curl-mcp `transfer` takes a dns URL too but has no switch for JSON: `statusCode` 0, `body.content` is the dig text.

## Gotchas
- Surl.Api: `server.Zones.AddZoneAsync(apex, text)`, `ReadAsync`, `ListAsync`; `SurlSeedRefusedException`
  (`Store` Zones; `Refusal` `a second zone for example.test` or `This instance holds no DNS zones`).
- Curl.Api (Curl c84035f797) has `CurlRequest.Dns = new CurlDnsOptions { Transport = "tcp", Json = true }` (namespace `Curl.Api`): `--dns-transport` and `--dns-json`; the default is UDP and dig text (older 0.2.0: text only; split record
  lines on tabs). Server side: `--allow-zone-transfer` has no Surl.Api property: `AdditionalArguments.Add(...)`; surl-mcp: `start_server` `arguments`. `JsonSerializer.Serialize` of anonymous types fails in a file-based app (reflection disabled): build
  with `JsonObject` and `JsonArray`, casting `(JsonNode)` when adding, and use `UnsafeRelaxedJsonEscaping` to keep quotes.
- Platforms: UDP to a port where nothing listens is exit 7 at once on Windows and retransmits on Linux and macOS
  (see platforms.md, CoAP; not probed for dns). No recipe here depends on it, so none is platform-marked.

## Recipes
dns-read-answers-as-json, dns-transfer-a-whole-zone (and twins)

- Decided by Surl (2026-11-22): `--allow-zone-transfer` writes no warning; it is a deployment choice like `--list-directories` (Surl ADR-0092; ADR-0046 d.5 warns only for test-only loosenings).

## Encrypted transports and pointing a client at a zone (batch 14, Surl d64faa4bb / Curl 2145a49eae, windows)
- Server: `--dns-transport tls` (DoT) or `https` (DoH, resource `/dns-query`) with `--cert/--key` (or `--self-signed`); no cert: exit 58 `dns://...needs a certificate: give --cert <file>, or --self-signed for a throwaway one`. `tcp,tls` on one port: exit 2 `--dns-transport tcp and tls cannot share port N`. `ready: port` works for both. Surl.Api: `AdditionalArguments = { "--dns-transport", "tls" }` + `SelfSigned`; no cert is `SurlServerStartException` ExitCode `CertificateProblem` (58). surl-mcp: `start_server` tool error, `exit_code` 58. The `-v` log shows DoH exchanges (TLS, ALPN http/1.1) but not the queries.
- curl lookups to a chosen server: `--doh-url https://127.0.0.1:P/dns-query --cacert server.pem` works (each lookup: 2 DoH connections, A and AAAA). `--doh-insecure` also works. No `--cacert`: exit 6 (not 60). `--dns-servers`: exit 2 `option --dns-servers: the installed libcurl version does not support this`. Without either, `.test` names: exit 6.
- curl `--dns-over-tls host:port` (extension, listed by `curl --extended-help`, not `--help all`) trusts the system store only: `--cacert`, `-k` ignored, so against a test cert it is exit 6 (surl logs `TLS handshake failed: unexpected EOF`). Use `dns://...` with `--dns-transport tls --cacert server.pem` (exit 60 without `--cacert`), then `--resolve`. `--dns-transport tcp` to a TLS-only port: exit 2 `Unable to read data from the transport connection`.
- Dig text over DoT/DoH carries an `OPT` line in ADDITIONAL (`.	0	CLASS1232	OPT	\# 0`; over DoH padded, `id: 0`); the question line also has `A` as field 4 with an empty field 5, so parse answers with `NF == 5`. A name under the apex not in the zone: `NXDOMAIN`.
- Curl.Api 0.2.0 in the p7 feed has `CurlDnsOptions` (Transport, Json); `CertificateAuthorityBlob` applies to dns:// over TLS and to DoH lookups; `CurlNetworkOptions.DohUrl`, `Resolve` work. Name failure: `CurlException` code 6 named `CouldntResolveHost`.
- Zones: no replace or per-record add; a second AddZoneAsync is refused; `ResetAsync` drops every seeded zone (ListAsync empty, names REFUSED); `SnapshotAsync`/`RestoreAsync` keep and restore zones. A child zone (`beta.example.test` beside `example.test`) answers its names; the parent's other names are unchanged.
- Recipes: dns-point-a-client-at-a-test-zone, dns-resolve-names-over-tls (and twins), dns-change-answers-between-tests, dns-seed-zones-for-a-test-class.
- Fixed in Curl f2331ebe (BL-5872, 2026-11-25): `--dns-over-tls host:port` trusts `--cacert` (and `-k`) like the transfer; with the test certificate one call resolves over DoT and fetches the page, exit 0. The dns-resolve-names-over-tls recipes now claim it.
