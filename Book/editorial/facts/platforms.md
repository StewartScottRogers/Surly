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
- Windows keeps every intermediate (and a root sent in the chain file) that .NET's `SslStreamCertificateContext` sees in the user's `CurrentUser\CA` (Intermediate Certification Authorities) store, and never removes them (2026-11-21). After about 25 runs of the chain triplet, whose setup makes a new `CN=Test Root` and `CN=Test Intermediate` with the same names each time, surl (every build, 9ad60d80c to 5da5b3eaf) failed to start on the leaf-plus-intermediate file with an unhandled `CryptographicException: An unknown chain building error occurred` in `X509Chain.Build` (exit 3762504530 or 127), console and Surl.Api alike; fresh names started at once. Cure on a dev machine, from PowerShell: `Get-ChildItem Cert:\CurrentUser\CA | ? Subject -in 'CN=Test Root','CN=Test Intermediate','CN=Other Intermediate' | Remove-Item`. CI runners start clean.
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
- One port number on both TCP and UDP (2026-11-21, Windows): Windows reserves blocks of 100 ports for one protocol only (`netsh int ipv4 show excludedportrange protocol=udp`), and free ports come in sequence, so binding one side to port 0 and the other to the same number failed `(45) ... Permission denied` five tries running. Pick the number at random below the free-port range (20000-40000) and retry (`https-fall-back-when-http3-is-missing-in-csharp`).

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
- macOS fetch through the socket (history): at Surl 9ad60d80c and a423807a5d surl crashed on its first Unix-socket accept on macOS (.NET `ArgumentException: Destination is too short` in `FinishOperationAccept`; first client 52 or 55, curl-mcp `CurlExit`, a Surl.Api program aborting 134 and leaving its socket file; CI run 44906624868, Surl BL-1565). Surl 5da5b3eaf fixes it (BL-1565: a catchable blocking accept; BL-1568: the accept polled so disposal ends it), so the triplet's fetch cases run on all three platforms again; macOS is confirmed only by CI. C# programs keep one socket file per case and `StopAsync` in `finally`.

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

- Unix sockets on macOS (diagnosed 2026-11-21, fixed in Surl 5da5b3eaf): see "Unix domain sockets" above; no `platforms:` needed any more.
- natss on Windows (2026-11-22, Curl c84035f797): `--cacert` needs `--ssl-no-revoke` too (exit 35 without), with a single self-signed certificate; the nats-log-in-with-a-token recipes pass `--ssl-no-revoke` everywhere. Linux and macOS not probed.
- `telnet://` as a raw XML client (xmpp-require-tls-before-login): `curl -m 3 -T file telnet://127.0.0.1:P` ends with exit 28 on Windows once -m passes; not yet seen on Linux or macOS.

## PowerShell and Pester (surl-run-a-fixture-from-pester, 2026-11-22)
- GitHub runner images (ubuntu-26.04 20261116, macos-26-arm64 20261117) have PowerShell 7.6 and Pester 5.9.0; windows images have Pester 3.4.0 and 5.9.0. This Windows machine has pwsh 7.6 with only Pester 3.4.0 (inbox). Pester 3.4 knows only `Should Be`, Pester 5 only `Should -Be`, so a test file for both throws instead. Checked by hand with Pester 5.7.1 saved into the scratchpad (PSModulePath); not installed.
- A BeforeAll that throws: Pester 3.4 counts 1 failure, Pester 5 fails each It (3). Claim only that the run fails.
- Start surl from pwsh with `System.Diagnostics.Process` and `RedirectStandardOutput`, then `StandardOutput.ReadLine()` for `Listening on`; no file polling, works the same on Unix. `Kill()` + `WaitForExit()` in AfterAll.

- A .NET program that starts `curl` with `Process`/`ProcessStartInfo("curl")` on Windows gets `C:\Windows\System32\curl.exe` (Windows searches System32 before PATH), not the port's curl; it fails `Protocol "webtransport" not supported`. `Process("surl")` is fine (nothing of that name in System32). Batch 11 C# recipes start only surl that way (`mcp-start-an-mcp-server-as-a-program-in-csharp`).
- WebTransport datagram limit (1,099-byte payload, 1,100 refused with exit 55) and the coaps PSK refusal (exit 35) are the port's own QUIC/DTLS code on both sides; probed on Windows only, not marked `platforms:` (batch 11, `webtransport-send-datagrams-and-streams`, `coaps-serve-over-dtls`).
- rediss, signalrs, socketios, stomps (batch 12, 2026-11-22, Windows only): no `platforms:` marks; the servers speak TLS from the first byte and the client speaks first (HELLO, HTTP negotiate, Engine.IO request, CONNECT), unlike natss where the server's plain INFO comes first (Curl BL-5750). Linux and macOS are for CI to confirm.

## Client certificates and TLS-SRP (batch 12, Curl c84035f797, 2026-11-22, Windows probed)
- Client certificate on Windows: curl, Curl.Api and curl-mcp read it from a P12 file only, as the Schannel build; PEM `--cert`/`--key` is exit 58 `schannel: Failed to import cert file` (Curl.Api `SslCertProblem`). Linux and macOS (OpenSSL build) are expected to take PEM; cases `[windows]` 58 and `[linux, macos]` 0 in the https-require-a-client-certificate triplet, not yet seen in CI. P12 is used everywhere else.
- A refused client certificate's curl exit code depends on TLS version and library (55/35/56 seen on Windows): claim only that it failed.
- TLS-SRP: no client on any platform (curl 8.22.0 dropped it; Curl ADR-0480). openssl s_client SRP fails on this Windows machine (MSYS2 3.5.9 segfault, Git for Windows 3.5.7 stall); Linux s_client unprobed.
- Recipes (batch 12): https-require-a-client-certificate (and twins), https-log-in-with-tls-srp (and twins).

- .NET server TLS refusal text differs by OS: a rejected client certificate logs "The remote certificate was rejected" on Windows and macOS, but "Authentication failed, see inner exception." on Linux (batch 12 CI). Claim the handshake failure on all platforms; mark the wording [windows, macos].

- Unix sockets on macOS (Curl BL-5686, measured 2026-11-22): a .NET server bound to a Unix-socket path shorter than 14 bytes can crash on accept, because macOS reports an unbound peer as a 16-byte address and .NET sizes the buffer to the listener's own path. Surl's fix is BL-1565. Give a socket path of 14 bytes or more, e.g. an absolute one ($PWD/surl.sock), to be safe on any build.

- macOS, a transfer surl aborts mid-body (`drop;after-bytes=N`): console curl and Curl.Api sometimes never see the reset and wait out --max-time or the timeout (batch 13 CI, run 45115316914; earlier for Curl.Api at an older Surl). Mark cut cases [windows, linux]. Timing: on loaded macOS runners a 250 ms timer gap can run past 450 ms; claim only gaps with a wide margin.
