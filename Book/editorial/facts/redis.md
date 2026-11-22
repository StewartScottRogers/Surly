<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# redis facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows; Curl.Api timing re-checked on Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20; databases and scenarios probed on those builds the same day.

## Listen URLs and readiness
`redis://host:port` (TCP, `ready: port`); `rediss://` with TLS. Keys live in memory, in databases 0 to 15, and go when the server stops.

## Server options that matter
- `--user alice:s3cret`: an account.
- `--allow-plaintext-auth`: lets the password cross plain `redis://`. Loosens (warning).

## Secure by default
- A client with no login is refused at its first command, HELLO: curl exit 67, `curl: (67) Redis server answered HELLO with error: NOAUTH ...`.
- A clear-text password without `--allow-plaintext-auth` gets WRONGPASS: kept on purpose (Surl ADR-0106); cite the Surl README, do not expect a change.

## Client: curl
The URL path is the command and its arguments: `redis://host:port/GET/greeting`; a number first selects a database (`/2/GET/greeting`); `-d` adds one last argument (`SET/greeting` with `-d changed` prints `OK`). A missing or expired key is null, written as an empty line, exit 0.

## Expiry and several URLs (probed 2026-11-20)
- `SET/k/v/PX/2000` and `/EX/3` work in the path; `TTL` seconds, `PTTL` ms, `-1` no expiry, `-2` no key or expired;
  `EXPIRE/k/1000` answers 1 (0 on a missing key), `PERSIST/k` 1, `EXISTS/k` 0 or 1. `SET/n/v/EX/0` and `GETEX` are exit 8.
- Several URLs in one `curl` run (`curl -s -u a:p URL1 URL2 ...`) run in order and fast: use one run for a
  set-then-read that must beat a short expiry. A separate curl start costs about a second on this machine, so
  never read a key in a second process a second or two after setting it (use EX 6 or more, or one run).
- Curl.Api: the first request in a cold `dotnet run` process took 4 s on an idle machine (0.4 s warm), and longer under
  load: a 6 s expiry read as null before it was due (Curl 7d167fa10a verify run). Read a live key under a long expiry
  (a minute), and to see it go set it again with a short one and wait past it; `Keys.SetAsync` again replaces the expiry.
- With `--user` and no `--allow-plaintext-auth`, `-u alice:s3cret` is exit 67 `WRONGPASS`.

## MCP
- `start_server`: `listen_urls`, `arguments` (surl's options); answers `server_id` and `bound_urls`.
- `set_key`: `server_id` (required), `key` or `key_base64`, `value` or `value_base64` (exactly one, else tool error `give exactly one of value and value_base64`), `database` (0-15), `expire_ms`. Needs no login. Answers `{"key":"greeting","database":0,"event_name":"0/greeting","byte_count":5}`; since Surl 763754785 `key` is bare (was `0/greeting`). On a server with no Redis listener (e.g. HTTP only): tool error `Keyspace: A key in database 0 was refused: This instance holds no Redis keyspace.`
- `read_key`: `server_id`, `key` or `key_base64`, `database`. Answers `type`, `byte_count`, and `text`, or `base64` when not UTF-8, plus `expires_at`. Missing or expired: tool error `No such key.`
- `set_key` `expire_ms` is schema-bounded 1 to 31536000000 (365 days): outside it the host answers an MCP invalid-params
  error (`McpProtocolException`, `Invalid params: arguments.expire_ms: expected at least 1`), not a tool result.
- `read_key` of a key with no expiry has no `expires_at` field at all. `list_keys` entries are `{key,type,byte_count}`
  plus `expires_at` only when the key has one (so a key with no expiry is one without the field).
- `get_events` with `kinds:["store_changed"]`, `store:"keyspace"`: `{change:"written", name:"0/app:session:1"}` per write,
  in order; `kinds:["login_judged"]`, `account`: `{account, is_accepted}` (a refused login is judged too).
- `list_keys`: `server_id`, `pattern` (Redis KEYS glob, default `*`), `after_key`, `limit` (1-1000, default 100), `database`. Answers `keys` (objects with `key`), in order, and `has_more`.
- curl-mcp `curl`: `args` (curl's own arguments); answers text that is JSON with `exitCode`, `standardOutput.content`, `standardError.content`.

## Gotchas
- Pass `-s` in curl-mcp's `args` to keep the progress meter out of `standardError`.
- An MCP C# program run by hand needs surl-mcp and curl-mcp on PATH (the env line does not add them); run its cases through verify.py.

## C#
Surl.Api `server.Keys.SetAsync(key, value, TimeSpan? expiry, database)` (expiry 1 ms to 365 days, else `ArgumentOutOfRangeException`),
`GetAsync` (`SurlKeyContents`: `Info.Type`, `Info.ExpiresAt` null if none, `Text`; `SurlNotFoundException` for a missing or expired key),
`ListAsync`. `SurlServerOptions.AllowPlaintextAuthentication` is `--allow-plaintext-auth`; `Accounts.Add(new SurlAccount("alice","s3cret"))`.
Curl.Api: `CurlRequest(HttpMethod.Get, $"{server.BoundUrl}GET/session")` with `Authentication = new CurlAuthenticationOptions{UserName, Password}`;
the body is the value plus a line feed, null is a lone line feed; a refused login throws `CurlException` code 67.
Curl.Api and a first request are slow to start (about a second or more): give an expiry at least 6 seconds.

## Databases (probed 2026-11-20, Surl 9ad60d80c / Curl 7d167fa10a)
- The first path segment, a number, selects the database: `/1/SET/k/v`, `/2/GET/k`; no number or `/0/` is database 0. A key of the same name in
  two databases holds two values; `DBSIZE`, `DEL`, `KEYS` (`%2A` for `*`) and `FLUSHDB` act on the named one only (`FLUSHDB` answers `OK`).
- Database 16 (and up): curl exit 8, `curl: (8) Redis server answered SELECT with error: ERR DB index is out of range`. `/-1/...` is read as a command (exit 8, unknown command).
- MCP `set_key` with `database: 16`: McpProtocolException `Request failed (remote): Invalid params: arguments.database: expected at most 15`.
  `read_key` and `list_keys` take `database` too (`list_keys` answers `{keys:[{key}], has_more}` for that database only).
- Surl.Api: `Keys.SetAsync(key, value, expiry, database)` outside 0-15 is `ArgumentOutOfRangeException`; `GetAsync(key, database)` outside is
  `SurlReadRefusedException`; `ListAsync("*", null, 100, database)` answers a `SurlKeyPage` (`.Keys` are `SurlKeyInfo`, `.Key`). Curl.Api to database 16 throws `CurlException` code 8.
- No login on `/1/GET/k`: exit 67 `NOAUTH HELLO must be called with the client already authenticated, ...` (long text; match the start). Curl.Api: code 67.

## Redis and HTTP in one surl
- `surl --user alice:s3cret --allow-plaintext-auth --directory site redis://127.0.0.1:P1 http://127.0.0.1:P2`: the one account covers both; HTTP with no login is 401
  (Digest challenge); `curl -u alice:s3cret http://...` works. `-w "%{http_code}"` trailing spaces are trimmed in verify's stdout: end each with a line feed in `-w`.
  One curl run applies `-u` and `-o` to every URL until `--next`.
- Surl.Api: `options.Files["products.json"] = bytes` plus two `ListenUrls`; `server.BoundUrls` are `Uri` (call `.ToString()`), in listen order. Curl.Api answers a 404 or 401 as a
  response (`StatusCode`), not an exception. surl-mcp `start_server` takes `files` with a redis and an http listener together; `bound_urls` in the same order.
- Expiry in a scenario: seed the short key only after a first (warm) request has been made; set 4 s, wait 5 s (recipes redis-seed-a-cache-for-a-test and twins).

## Recipes
redis-seed-keys-from-an-agent, redis-expire-a-key (and twins), redis-inspect-keys-an-app-left, redis-keep-keys-apart-in-databases (and twins), redis-seed-a-cache-for-a-test (and twins), redis-test-a-session-that-expires (and twins)

## Session expiry scenario (redis-test-a-session-that-expires and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- Seed `EX 6` (console `SET/session:abc/alice/EX/6`, Surl.Api `Keys.SetAsync(k, v, TimeSpan.FromSeconds(6))`, surl-mcp `set_key expire_ms 6000`), ask the app at once, then poll once a second at most 15 times until it reads null: passes on all three surfaces; C# and agent make one warm-up request before seeding.
- After expiry: `TTL` -2 and `EXISTS` 0 in one curl run; Surl.Api `GetAsync` `SurlNotFoundException`; surl-mcp `read_key` tool error `No such key.`. `SET/k/v/EX/60` then `TTL` answers 59 or 60. surl-mcp `start_server` takes `files` with a redis and an http listener; one `--user` covers both.
