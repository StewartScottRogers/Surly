<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# mcp facts (MCP as a protocol surl serves; not surl-mcp / curl-mcp as hosts)
Checked against: surl 1.0.0 (Surl 5da5b3eaf), curl 8.22.0 (Curl c84035f797), Surly.Curl.Api 0.2.0, 2026-11-22, windows.

## Listen URLs and readiness
- `mcp://host:port/` plain HTTP/1.1, `mcps://` TLS from the first byte (needs `--cert`/`--self-signed`, else 58); TCP, `ready: port`. Endpoint `/mcp`; any other path 404 (curl exit 8).
- `mcp+stdio:` serves one session on surl's own stdin/stdout, newline-delimited JSON-RPC; log lines on stderr. Exits 0 when its input ends. No host/port/path. Only listen URL of its run: beside another, exit 2 `surl: mcp+stdio: serves standard input and output and cannot be combined with another listen URL`. `--log-file -`/`--trace -`/`--trace-ascii -`: exit 2 `surl: mcp+stdio: standard output carries only MCP messages, so --trace, --trace-ascii and --log-file cannot write to '-'`. `--fault`: exit 2 `surl: mcp+stdio: takes no --fault, because an mcp+stdio session is outside the event stream and the fault rules`.
- Fed by hand: `surl --directory site mcp+stdio: < session.jsonl` (initialize, notifications/initialized, ping) prints the initialize result (`serverInfo.name` surl) and `{"jsonrpc":"2.0","id":2,"result":{}}`; nothing for the notification.

## Server options that matter
- Default tools `read_file` (path), `echo` (text); `--list-directories` adds `list_directory`, `--allow-uploads` adds `write_file` (`path`, and `text` or `base64`; not `content`, which is -32602 `arguments.content: not allowed`). write_file answers `wrote N bytes to <path>`.
- `--user :TOKEN` (empty user name) is a Bearer token account; `--oauth2-bearer TOKEN` logs in.
- mcp+stdio takes no login: with `--user` accounts and no `--allow-anonymous`, tools still listed; option gates still apply.

## Secure by default
- No login: 401, curl exit 67. Wrong token: 401, 67. `-u user:pass` (even a real account): 400, exit 8.
- Right token over plain `mcp://`: 401 (refused unchecked), 67; `--allow-plaintext-auth` makes it accepted. `--allow-anonymous` (with accounts): no-login client lists tools.
- mcps without `--cacert`: 60. Windows: `--cacert` alone verifies (no `--ssl-no-revoke` needed).

## Client: curl
- `curl URL/mcp` lists tools (name TAB description per line); `-d @call.json` (`{"name":..,"arguments":{..}}`) calls one, each content item on its own line (a file's own newline plus curl's = two).
- `mcp+stdio://localhost/<program>?arg1&arg2` starts the program from PATH. Program not found: exit 7 `curl: (7) Failed to start the MCP server <name>: ...` (.NET's text after the colon). No host: exit 3.

## C#
- Curl.Api 0.2.0: a request to `mcp(s)://.../mcp` or `mcp+stdio://...` lists the tools as the body (StatusCode 0), but **never sends a Body as tools/call** (GET/POST/PUT, bytes or stream: still the list). Bearer: `CurlAuthenticationOptions.XOAuth2Bearer`. Refusals: `CurlException` LoginDenied 67, WeirdServerReply 8 (password), PeerFailedVerification 60. Curl.Api starting surl from mcp+stdio passes surl's stderr through to the program's stderr.
- Surl.Api: `ListenUrls = { "mcp+stdio:" }` throws `UriFormatException` (not SurlServerStartException). mcps in-process works (`CertificateFile`, `KeyFile`, `Accounts.Add(new SurlAccount("", "tok"))`).
- To call a tool from C#: `Process` (`ProcessStartInfo("surl")`, ArgumentList, redirect all three, read stderr in the background) and write JSON-RPC lines (`mcp-start-an-mcp-server-as-a-program-in-csharp`).

## MCP
- surl-mcp `start_server` with `listen_urls: ["mcp+stdio:"]`: JSON-RPC Internal error (-32603), not a tool error. mcp/mcps work, options in `arguments`.
- An agent's own MCP SDK client starts surl directly: `StdioClientTransport { Command = "surl", Arguments = [..., "mcp+stdio:"], WorkingDirectory }`; `ServerInfo.Name` surl; unknown tool is `McpProtocolException` -32602; a missing file is a result with `IsError` true, text `no such file or folder`.
- curl-mcp `curl` tool with an `mcp+stdio` URL starts surl from PATH and calls (`-d '<json>'` as one list item). `transfer` with mcp(s) or mcp+stdio lists (statusCode 0); `headers` must be a JSON array of `{name,value}` (from C# pass a `JsonElement`; a `Dictionary<string,string>[]` throws NotSupportedException in the SDK's serializer).

## Recipes
mcp-call-a-tool, mcp-start-an-mcp-server-as-a-program (and twins), mcp-require-a-login-for-tools (and twins).

## Re-pin 3: Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae, Surly.Curl.Api, 2026-11-22, windows
- Curl.Api now sends a request `Body` as the `tools/call` (BL-5758), to mcp, mcps and mcp+stdio: POST with the call's JSON answers the content items, one per line (`hello from surl` + 2 newlines for read_file), StatusCode 0. No Body still lists the tools. JSON-RPC error is `CurlException` WeirdServerReply 8, message `MCP server error -32602: Invalid params: unknown tool write_file`; a program that cannot be started is CouldntConnect 7, `Failed to start the MCP server <name>: ...` (rest of the message is .NET's, platform-specific). With the Bearer token a call goes through; without, LoginDenied 67 as for the list. Curl.Api's mcp+stdio needs no Process: it starts surl from PATH itself, surl's stderr passes to the program's stderr.
- Surl.Api `StartAsync` with `ListenUrls { "mcp+stdio:" }` now throws `SurlServerStartException`, ExitCode FailedInit (2), message `mcp+stdio: serves the standard input and output of a surl process and cannot be started in-process; listen on mcp://127.0.0.1:0/ instead` (was UriFormatException).
