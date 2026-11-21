<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# platforms facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19; windows run
locally, linux (ubuntu-latest) and macos (macos-latest) from CI run 44662810792 of the same builds.

Only Windows runs locally; CI runs all three. A case whose claim holds on some platforms only gets
`platforms: [...]` and its claim says which; add a case for the other platforms with what they do,
so the recipe still claims something everywhere. Never loosen an `expect` to cover two behaviours
unless the claim is the part they share.

## LDAP: the client library under curl
The Curl port answers as curl's two reference builds (Curl ADR-0166, `LdapDialect`):
- Windows: curl's `lib/ldap.c` over WinLDAP. Every failed bind is exit 38 with WinLDAP's text for the
  server's result: `Invalid Credentials`, `Confidentiality Required`, `Inappropriate Authentication`,
  `Authentication Method Not Supported` (`LDAP local: bind via ldap_win_bind ...`; Curl.Api message the same).
  `--ntlm` and `--negotiate` binds work (Sicily NTLM; GSS-SPNEGO carrying NTLM). No `-u`: binds Negotiate
  as the Windows user.
- Linux and macOS: curl's `lib/openldap.c`. A failed simple bind never names the server's result:
  `invalidCredentials` is exit 67 `Login denied` (Curl.Api `LoginDenied`); every other refusal is
  exit 38 `LDAP: cannot bind` (`LdapCannotBind`). No `-u`: an anonymous bind (refused 38 by surl).
  SASL only DIGEST-MD5: `--ntlm` or `--negotiate` reads the root DSE and gives up with exit 67
  `Login denied` whatever the server offers. A server that closes: exit 7.
- Output layout: OpenLDAP adds one `\n` after each entry; root DSE line `DN:` with no space.
- ldaps certificate check: Windows uses the system store (`--cacert`/`CertificateAuthorityBlob` do not
  help, exit 60); Linux and macOS honour the blob (Curl.Api trusted search succeeded in CI) and say
  `SSL certificate OpenSSL verify result: self-signed certificate (18)` without it.
So: a claim that reads the server's result from stderr is `platforms: [windows]`, with a
`[linux, macos]` twin case checking the exit code (38 or 67) and `LDAP: cannot bind` / `Login denied`.
A wrong password over a plain listener that refuses unread is 38 on Linux, not 67: that is how a
Linux case shows the password was not checked.

## TLS
- curl (all platforms): the port's own TLS client; TLS 1.2 and 1.3 everywhere. `curl: (35)` wording
  differs by platform (Windows mimics Schannel), so match `curl: (35)` or the exit code only.
- surl server: .NET `SslStream`. Windows (Schannel) and Linux (OpenSSL) serve TLS 1.2 and 1.3.
  **macOS serves TLS 1.2 at most** (Surl ADR-0010): the default range negotiates 1.2, a client that
  insists on 1.3 (`--tlsv1.3`, `SslVersion.Tls13`) gets exit 35, and `surl --tlsv1.3` starts but fails
  every handshake (exit 35). Mark cases that need a TLS 1.3 handshake `platforms: [windows, linux]`
  and add a `[macos]` case showing the 35.
- Certificate chains (2026-11-20, Windows probed; Linux and macOS expected, CI checks): Schannel needs `--ssl-no-revoke` to accept a root, intermediate and leaf chain whose certificates carry no revocation address (else exit 60 `schannel: the revocation status is unknown`), and it keeps intermediates it has seen, so a leaf-only server of an intermediate that another server sent verifies on that machine. `https-serve-a-certificate-chain` uses `--ssl-no-revoke` everywhere and a second intermediate for the leaf-only case; the `60` wording is the library's, so cases match the exit code. The `58` message after the prefix `Could not load the server certificate: ` is .NET's, so cases match the prefix.
- Curl.Api `CertificateAuthorityBlob` (CI run 44835381862, Curl 7d167fa10a; cause read in Curl's `TlsClientOptions.HasCaCertificates`): an empty blob is not "no blob". Windows trusts nothing from it (exit 60); Linux and macOS (OpenSSL build) refuse a CA bundle holding no certificate, `SslCacertBadfile` 77. C# trap: `cond ? null : File.ReadAllBytes(f)` is a `byte[]`, and its null converts to an empty `ReadOnlyMemory<byte>`, not to null; write `new ReadOnlyMemory<byte>(...)` in the other branch (`https-serve-a-certificate-chain-in-csharp`).
- gRPC `-m` (Curl 7d167fa10a): curl also sends it as `grpc-timeout`, and surl ends the call `DEADLINE_EXCEEDED` when it passes, so `-m 1` against a reply held back 3 s races the two timers: exit 28 on Windows and Linux, but curl-mcp on macOS got exit 22 `grpc-status 4 (DEADLINE_EXCEEDED): deadline exceeded` (CI run 44835381862). `-H "grpc-timeout:"` keeps the header off, so only curl stops: exit 28 everywhere (`grpc-test-a-failing-call-from-an-agent`). The console twin `grpc-test-a-failing-call` has the same fix; the C# twin sends no `grpc-timeout` (HttpClient with its own cancellation), so it has no race.
- HTTP/3 and WebTransport use the port's own QUIC TLS 1.3 on both sides and pass on macOS.

- Alt-Svc `h3` entries (batch-07, Curl 7d167fa10a, Windows probed): with no version option, Windows `curl`, Curl.Api and curl-mcp never act on an `h3` entry naming the origin (Curl ADR-1313, as the Schannel build, which has no HTTP/3); Linux and macOS race QUIC against TCP (ADR-1309). The `https-move-a-client-to-http3` triplet's solutions pass `--http3` (`HttpVersion.Http3`) and run everywhere; a `[linux, macos]` case shows the switch with no version option (not yet seen in CI) and a `[windows]` case the stay on HTTP/2.

## UDP and QUIC to a port where nothing listens
- Windows: the ICMP port unreachable comes back to the socket; curl exits 7 at once (CoAP, WebTransport,
  HTTP/3).
- WebTransport (QUIC) on Linux and macOS: exit 55 (send failure), not 7. Mark the 7 `[windows]`, add a
  `[linux, macos]` case expecting 55.
- CoAP on Linux and macOS: no failure at once; curl keeps retransmitting and outlived verify's 60 s
  timeout. Mark the case `[windows]`; no Linux case is practical. `--retry 5 --retry-all-errors
  --retry-delay 1` still works there (the retransmission reaches surl once it is up).
- DNS (`dns://`): a query to a live surl DNS server works on Windows; no recipe claims a platform difference yet. A query to a port where nothing listens is expected to behave like CoAP above (not probed). `dns` also listens on TCP, so `ready: port` is safe for it.
- Readiness for UDP servers stays `ready: none` everywhere.

## Paths and shells
- `-o NUL` is Windows only in prose; cases use `{{null}}`.
- Stopping a backgrounded surl from `sh` (2026-11-20, Windows probed; Linux/macOS from surl's help, CI checks):
  Linux and macOS: `kill $pid; wait $pid` gives 0 (surl handles SIGTERM). Windows Git Bash: `kill` (TERM)
  ends the native surl outright after about 3 s, `wait` gives 143. `kill -INT` does nothing to it (surl keeps
  serving; a later TERM then took 19 s and gave 130): never use INT. `kill -9` gives 137 everywhere. So a
  script that judges surl's exit code accepts 0 or 143 (`surl-run-a-test-server-in-ci`); cases that show the
  code are `[linux, macos]` 0 and `[windows]` 143.
- A trapped signal interrupts `wait` at once but waits for a foreground command: run the tests with `&` and
  `wait` so a TERM to the script acts at once (`surl-start-and-stop-in-a-shell-script`).

- Git Bash (MSYS) converts arguments that look like POSIX paths, `path=/backups` included, into Windows paths before a native program sees them; a `--fault` scoped by `path=` then never matches. Set `MSYS_NO_PATHCONV=1` (2026-11-20, `ftp-run-out-of-space-on-upload`). Recipe scripts that pass such arguments need it on Windows.

## Unix domain sockets
- surl `--unix-socket`, curl `--unix-socket`, Curl.Api `UnixSocketPath`, surl-mcp and curl-mcp all work on Windows (AF_UNIX); no `platforms:` needed for serving and fetching. What differs is stopping: Git Bash's `kill` ends surl outright on Windows, leaving the socket file (next start 45); Linux and macOS SIGTERM lets surl remove it (`http-listen-on-a-unix-socket`, cases `[windows]` and `[linux, macos]`). Path limit 107 bytes (Linux, Windows), 103 (macOS): use relative paths.
- macOS fetch through the socket fails (CI run 44906624868, surl 1.0.0 / Surl 9ad60d80c, Curl 7d167fa10a; reported to Surl): surl binds the file and prints `Listening on ... over unix://surl.sock`, then `curl -s --unix-socket surl.sock http://localhost:<port>/hello.txt` exits 55; curl-mcp's `curl` tool fails `CurlExit`; a Surl.Api plus Curl.Api program aborts (exit 134), leaving its socket file, so the next case on that path gets `BindFailed (45) ... Address already in use`. Not the path length: the paths are relative (9 bytes), the connect succeeds (a long path would be curl exit 6, nothing listening exit 7), and the same relative paths pass on Linux. Cases that only bind, refuse or stop pass on macOS. Fetching cases are `[windows, linux]`, with a `[macos]` case claiming the 55 (`http-listen-on-a-unix-socket` and twins). C# programs: one socket file per case and `StopAsync` in `finally`.

## Unacknowledged writes
- MQTT QoS 0 through curl or curl-mcp: curl returns before the broker keeps the message, and on macOS a
  `read_retained_message` straight after answered the previous value or nothing (CI run 44835381862). After each
  publish, `wait_for_event(kinds:["exchange_ended"], after_sequence: <last>)`: the exchange ends after the
  broker has applied what the client sent, and it ends for refused and unretained publishes too, so one wait
  fits every case (`mqtt5-check-a-retained-message-from-an-agent`, `mqtt-check-retained-state-after-a-run`).
  `store_changed` on `bus` with the topic as `name` also works but never comes when nothing is kept.

## Recipes
ldap-search-a-directory, ldap-bind-with-ntlm, ldap-search-with-a-filter-and-scope, ldaps-search-over-tls
(and twins), https-refuse-clients-below-tls-1-3 (and twins), coap-get-a-resource, webtransport-open-a-stream,
surl-start-and-stop-in-a-shell-script, surl-run-a-test-server-in-ci, https-serve-a-certificate-chain (and twins), https-move-a-client-to-http3 (and twins),
grpc-test-a-failing-call-from-an-agent, mqtt5-check-a-retained-message-from-an-agent, mqtt-check-retained-state-after-a-run,
https-fall-back-when-http3-is-missing-from-an-agent, sftp-offer-a-single-cipher, http-multiplex-requests-over-http2-in-csharp,
http-download-a-file-in-parallel-ranges, http-stream-a-download-and-hash-it, http-download-in-parallel-segments, http-listen-on-a-unix-socket (and twins), telnet-hold-a-line-session-in-csharp.

- Alt-Svc on Windows (Curl ADR-1313, confirmed by the Curl session 2026-11-20): with no version option, curl, Curl.Api and curl-mcp ignore a stored h3 or h2 Alt-Svc entry, as the Windows reference curl 8.22.0 (Schannel) does. With `--http3` (or `--http2`; Curl.Api `Http3`) given, a stored h3 entry is used on Windows too, so a recipe that moves a client to HTTP/3 through Alt-Svc passes `--http3` on every platform.

## CI fixes from run 44871788391 (batch-07, Curl 7d167fa10a; Windows verified, Linux and macOS from the CI log)
- curl-mcp `--http3-only` to a TCP-only https server (nothing on UDP): Windows 56 at once; Linux and macOS never hear the port unreachable and outlived verify's 60 s (curl's 300 s connect default, Curl BL-5659), although the console `curl` got 56 there. Bound it with `--connect-timeout 2` and case `[windows]` 56, `[linux, macos]` 28 (`https-fall-back-when-http3-is-missing-from-an-agent`).
- macOS's own OpenSSH (10.5p1, LibreSSL) reads `/etc/ssh/crypto.conf` and prefers `aes128-gcm@openssh.com`; stock OpenSSH on Windows and Linux picks `chacha20-poly1305@openssh.com` against surl's default list. Both show `MAC: <implicit>`. A claim about the client's default cipher is per platform (`sftp-offer-a-single-cipher`).
- BSD `wc -c` (macOS) pads its count with spaces; use `wc -c < f | tr -d ' '` in scripts whose output is matched.
- Connections opened without `PipeWait` (Curl.Api, HTTP/2, five requests at once): 5 on Windows, 2 on Ubuntu, 1 on macOS. A race: claim a range, never an exact count. A count of connections over HTTP/1.1 is exact only when the requests overlap: macOS reused a finished one (4, not 5, CI run 44878668055); hold each request with a 1 s `delay;stage=command` fault.
- `SurlFault.DropConnection() with { AfterBytes = ... }` mid-body: on macOS the Curl.Api body read never ended (60 s timeout); Windows gets `CurlException` `RecvError` at once. `CurlClientOptions.Timeout` covers the streamed body as well (Windows: a slowed reader got `CurlException` at the timeout), so a bound makes it a `CurlException` everywhere (`http-stream-a-download-and-hash-it`).

## CI fixes from run 44898744862 (Curl 7d167fa10a; Windows verified, Linux and macOS from the CI log)
- A `drop` fault at the accept stage (no `stage`), before the greeting: Windows `curl` and Curl.Api always got 56 `RecvError`; Linux and macOS console `curl` got 7 `Could not connect to server`, and Curl.Api on Linux got 56 on one connection and 7 `CouldntConnect` on the next in the same run. A race: claim "fails, 7 or 56" (`smtp-resend-after-a-dropped-connection` and its C# twin). `--retry` alone retries neither. A drop at `stage=command` comes after the connection is made: 56 everywhere. The agent twin accepts both too.
- `curl -v` over FTP: the command order is the same on all three (PWD, CWD..., EPSV, TYPE I, SIZE, REST, RETR), but the Windows trace as verify reads it has a blank line after each line and Linux and macOS have none, so a `stderr-contains` spanning two trace lines passes only on Windows. Pipe `2>&1 | grep` for the lines the claim needs and match `stdout` exactly (`ftp-choose-how-curl-walks-folders`, `ftp-resume-an-interrupted-download`).

## CI fixes from run 44906624868 (batch-09, Surl 9ad60d80c / Curl 7d167fa10a; Windows verified, Linux checked in WSL and Docker)
- ubuntu-latest is now Ubuntu 26.04, whose coreutils are uutils (Rust), not GNU: `sort -t=` (separator glued to the option) is refused `sort: separator must be exactly one character long: ''` (exit 2, nothing on stdout), so a pipeline ending in it prints nothing on Linux only. Write `sort -t '=' ...` with the separator as its own argument (`http-download-in-parallel-segments`). Other glued short-option arguments are suspect too; WSL's Ubuntu 26.04 reproduces it (`printf 'a=2\nb=1\n' | sort -t= -k2n`).
- Telnet through Curl.Api, server closing after `line too long`: the close comes 5-50 ms after the text on Windows and in a Linux container on one CPU, but a 1 s silence window missed it once on a loaded ubuntu runner. A program that tells "closed" from "went quiet" waits 5 s of silence (`telnet-hold-a-line-session-in-csharp`).

- Unix sockets on macOS (diagnosed 2026-11-21): surl at 9ad60d80c crashes on its first accept over a Unix socket (.NET `ArgumentException: Destination is too short` in `FinishOperationAccept`), so the first client sees an empty reply or a send failure and every later connect is refused. Surl BL-1565 owns it; curl is not at fault. Until it is fixed, Unix-socket fetch cases are marked off macOS.
