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
- `--allow-zone-transfer`: loosens; AXFR/IXFR (not probed).

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
- Curl.Api 0.2.0 answers `dns://` with the dig text only (no `--dns-json` property); read the body, split record
  lines on tabs. `JsonSerializer.Serialize` of anonymous types fails in a file-based app (reflection disabled): build
  with `JsonObject` and `JsonArray`, casting `(JsonNode)` when adding, and use `UnsafeRelaxedJsonEscaping` to keep quotes.
- Platforms: UDP to a port where nothing listens is exit 7 at once on Windows and retransmits on Linux and macOS
  (see platforms.md, CoAP; not probed for dns). No recipe here depends on it, so none is platform-marked.

## Recipes
dns-read-answers-as-json (and twins)
