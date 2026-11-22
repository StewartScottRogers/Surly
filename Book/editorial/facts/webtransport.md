<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# webtransport facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`webtransport://host:port/`, UDP only (HTTP/3 over QUIC), so `ready: none`; a TCP connection to the port is refused (curl exit 7). Any path accepts a session; surl echoes each stream and datagram.

## Server options that matter
- `--cert file --key file` (made in `setup:` with openssl), or `--self-signed` (curl `-k`). Required.
- `--allow-anonymous`: sessions without a login. Loosens (warning). Since Surl 763754785 it is needed even on a server with no account.
- `--user alice:secret`: an account.

## Secure by default
- No certificate: surl exits 58 at start, `surl: (58) webtransport://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one`.
- No login without `--allow-anonymous`, with or without accounts: the CONNECT is answered 401, curl exit 22, stderr `The server refused the WebTransport session: HTTP status 401`. A wrong password: the same, exit 22.
- curl without `--cacert` for the server's certificate: exit 60.

## Client: curl
- `-d data webtransport://host:port/path`: bidirectional stream, the echo written with no newline. `?wt=uni` and `?wt=datagram` for the other modes.
- `-u user:pass` logs in. `--http3-only https://...` to the port is not a session: 405.
- Nothing listening on the UDP port: exit 7 at once on Windows (ICMP); exit 55 on Linux and macOS (CI run 44662810792).
- `--retry 5 --retry-all-errors --retry-delay 1` waits out surl's start.

## MCP
- surl-mcp `start_server` `listen_urls: ["webtransport://127.0.0.1:0"]`, `arguments: ["--cert","server.pem","--key","server.key","--user","player1:secret"]`; no certificate: tool error, `exit_code` 58 `CertificateProblem`.
- curl-mcp `curl` tool does every mode (`-d`, `?wt=uni|datagram`, `-u`); payloads via `stdin` + `--data-binary @-`; `standardOutput.totalBytes` counts the echo. `wait_for_event(kinds:["login_judged"])` answers `{"event":{..."account","is_accepted"}}`.

## Datagram size and modes (batch 11, Curl c84035f797, windows)
- A datagram payload of 1,099 bytes is echoed; 1,100 is refused by curl before sending, exit 55 `The data does not fit in one datagram: at most 1100 bytes, quarter stream ID included`. Streams carry 100,000 bytes whole. `-w "%{size_upload} %{size_download}"` works. Unknown `wt=` value: exit 3 `Unknown WebTransport mode "wt=x": use wt=bidi, wt=uni or wt=datagram`.

## C#
- Surl.Api hosts `webtransport://127.0.0.1:0` (CertificateFile/KeyFile; none: SurlServerStartException 58). `RecordExchanges = true`: chunks with `Layer == ExchangeBytesLayer.Decoded` hold the header blocks as text (`:method: CONNECT`, `:protocol: webtransport-h3`, `:path`, `authorization: Basic ...`; sent `:status: 200` or `401` with Digest challenges). LoginJudged (Basic) can come after SendAsync throws: WaitForEventAsync.
- **Curl.Api 0.2.0 sends no data on a WebTransport session**: CONNECT and login work (401 is `CurlException` HttpReturnedError 22 `The server refused the WebTransport session: HTTP status 401`), but a Body (bytes or stream, any `wt=`) is never sent; StatusCode 0, empty body. .NET has no WebTransport client; System.Net.Quic has no datagrams.
- A .NET `Process` named `curl` on Windows starts System32's curl.exe (`Protocol "webtransport" not supported`), not the port's.

## Gotchas
- `build/probe.py` and verify pick ports free for TCP; Windows reserves UDP ranges (`netsh int ipv4 show excludedportrange protocol=udp`), so surl may exit 45 `Could not bind ... Permission denied`. Read the surl log line and retry.
- probe.py runs in a temp folder and splits on spaces: probe with `--self-signed` and `-k`, not a certificate path with spaces.
- On Curl 82913be53d about 1 run in 30 under load failed with exit 56 (datagram channel aborted); not rechecked.

## Recipes
webtransport-open-a-stream, webtransport-send-datagrams-and-streams (and twins)
