<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-22 (batch 11 of the 1,000-recipe plan)

From writing and verifying batch 11 (62 recipes) of the *Surly* cookbook against Surl `5da5b3eaf`, Curl `c84035f797`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | Surl.Api | `SurlServer.StartAsync` with the listen URL `mcp+stdio:` throws `UriFormatException`; a `SurlServerStartException` (exit 2) was expected |
| 2 | Low | `surl-mcp` | `start_server` with `listen_urls: ["mcp+stdio:"]` answers a JSON-RPC Internal error; a tool error with exit code 2 was expected |
| 3 | Low | DNS | `--allow-zone-transfer` prints no warning at start, unlike the other loosening options |
| 4 | Question | STOMP and retain | A STOMP subscriber to a retained topic is not sent the retained message, while an MQTT subscriber is. Intended? |
| 5 | Check | STOMP | One probe's queue SUBSCRIBE (curl with no `-m`) hung for over 120 s; not reproduced since. Recipes now bound every STOMP subscribe |

## Repros

**1.** `new SurlServerOptions { ListenUrls = { "mcp+stdio:" } }` then `StartAsync` → `UriFormatException`.
**2.** surl-mcp `start_server` `{"listen_urls": ["mcp+stdio:"]}` → JSON-RPC error -32603.
**3.** `surl --directory site --allow-zone-transfer dns://127.0.0.1:5353/` (zone files in `site/`, as in `dns-transfer-a-whole-zone`): no stderr line; compare `--allow-anonymous`.
**4.** `surl --topic "home/#;retain" mqtt://127.0.0.1:1883/ stomp://127.0.0.1:61613/`, publish retained `home/t` over MQTT, then subscribe to `home/t` over STOMP: no message; an MQTT subscribe gets it.
