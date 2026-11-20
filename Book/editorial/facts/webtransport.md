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
- Nothing listening on the UDP port: exit 7 at once on Windows (ICMP); may differ elsewhere.
- `--retry 5 --retry-all-errors --retry-delay 1` waits out surl's start.

## MCP
Not probed.

## Gotchas
- `build/probe.py` and verify pick ports free for TCP; Windows reserves UDP ranges (`netsh int ipv4 show excludedportrange protocol=udp`), so surl may exit 45 `Could not bind ... Permission denied`. Read the surl log line and retry.
- probe.py runs in a temp folder and splits on spaces: probe with `--self-signed` and `-k`, not a certificate path with spaces.
- On Curl 82913be53d about 1 run in 30 under load failed with exit 56 (datagram channel aborted); not rechecked.

## Recipes
webtransport-open-a-stream
