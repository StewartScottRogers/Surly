<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# redis facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

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

## MCP
- `start_server`: `listen_urls`, `arguments` (surl's options); answers `server_id` and `bound_urls`.
- `set_key`: `server_id` (required), `key` or `key_base64`, `value` or `value_base64` (exactly one, else tool error `give exactly one of value and value_base64`), `database` (0-15), `expire_ms`. Needs no login. Answers `{"key":"greeting","database":0,"event_name":"0/greeting","byte_count":5}`; since Surl 763754785 `key` is bare (was `0/greeting`). On a server with no Redis listener (e.g. HTTP only): tool error `Keyspace: A key in database 0 was refused: This instance holds no Redis keyspace.`
- `read_key`: `server_id`, `key` or `key_base64`, `database`. Answers `type`, `byte_count`, and `text`, or `base64` when not UTF-8, plus `expires_at`. Missing or expired: tool error `No such key.`
- `list_keys`: `server_id`, `pattern` (Redis KEYS glob, default `*`), `after_key`, `limit` (1-1000, default 100), `database`. Answers `keys` (objects with `key`), in order, and `has_more`.
- curl-mcp `curl`: `args` (curl's own arguments); answers text that is JSON with `exitCode`, `standardOutput.content`, `standardError.content`.

## Gotchas
- Pass `-s` in curl-mcp's `args` to keep the progress meter out of `standardError`.
- An MCP C# program run by hand needs surl-mcp and curl-mcp on PATH (the env line does not add them); run its cases through verify.py.

## Recipes
redis-seed-keys-from-an-agent
