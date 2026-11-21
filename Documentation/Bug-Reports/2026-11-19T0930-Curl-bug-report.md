<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-19 09:30 (UTC-7)

These findings come from writing and verifying Part 5 (New Protocols) of the *Surly* cookbook (`Z:\repos\Surly\Book`), which covers the Curl port's extended schemes. The report comes on top of `2026-11-19T0642-Curl-bug-report.md`.

**Build tested:** Curl at commit `82913be53d` (2026-11-19 06:10): the binary reports `curl 8.22.0`, with `curl-mcp` built from it. Windows 11, .NET SDK 11.0.100. The server was Surl at `71807f05f`. Finding 1 was reproduced again just before this report was written; the others come from passing or deliberately failing recipe cases and the writers' exact commands.

The port's extended clients worked end to end against Surl for every scheme in the chapter list: s3, gs, az, oci, mqtt5 (including `mqtt5s`), amqp/amqps and amqp1s, kafka/kafkas, nats, stomp, xmpp with STARTTLS, grpc (reflection and `--grpc-proto`), signalr, socketio, webtransport (stream, `?wt=uni`, `?wt=datagram`), mcp/mcps/mcp+stdio, coap/coaps, dns (UDP, TCP, `--dns-json`), and redis/rediss.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | `curl-mcp` | The `curl` tool refuses the port's own extended options (`--dns-json`, `--dns-transport`) as unknown |
| 2 | Medium | WebTransport | Under load, a plain echo fails about 1 time in 30 with "the datagram channel failed", though no datagram is used |
| 3 | Low | SignalR | The client sends no keep-alive, so a listener with no `-d` is closed by the hub after 30 s (exit 22) |
| 4 | Low | Socket.IO | The client ignores `-u`; it can join only a server with `--allow-anonymous` |
| 5 | Low | AMQP 0-9-1 | The client logs in only with PLAIN, so it can't join a server that offers ANONYMOUS or CRAM-MD5 but not PLAIN (exit 67) |
| 6 | Low | Azure | The `az` client does not do Shared Key signing: `-u` sends no credentials, so only `--oauth2-bearer` logs in |

---

### 1. `curl-mcp` refuses extended options
**Repro:** call the `curl` tool with `{"args": ["-s", "--dns-json", "dns://127.0.0.1:53/www.example.com"]}`, or with `--dns-transport tcp`.
**Observed:** `failure: {"kind": "PathRefused", "text": "Refused --dns-json: --dns-json is not an option this server knows, so it cannot tell whether it uses a local path."}`. The same happens for `--dns-transport`.
**Expected:** curl runs, as on the console, where both options work. The path-confinement check seems to know only the original options. Options that name no file (`--dns-json`, `--dns-transport`) should pass, and the option table should cover the extended ones. Agents using `curl-mcp` for DNS are limited to text output.

### 2. WebTransport: an intermittent "datagram channel" failure
**Repro:** `curl --cacert server.pem -d "hello over a stream" webtransport://127.0.0.1:<port>/echo` against `surl --cert ... --key ... webtransport://127.0.0.1:<port>/`, with three such loops running side by side.
**Observed:** about 1 run in 30 exits 56 with `WebTransport session failed: QUIC: the datagram channel failed: The I/O operation has been aborted...`, though the exchange sends no datagram. Run alone it passed 40 of 40.
**Expected:** a stream exchange should not fail because of the datagram channel. It looks like the datagram reader is torn down, or raced, while the session is still in use.

### 3 to 6
These are as summarised in the table. The cookbook recipes `signalr-receive-a-message`, `socketio-receive-an-event`, `amqp-send-and-receive` and `az-put-and-get-a-blob` each have a case showing the behaviour, with the exact command in their `review.md`.
