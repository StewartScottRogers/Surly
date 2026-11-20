<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-19 06:42 (UTC-7)

These findings come from rewriting the MCP recipes and writing Part 4 (Advanced) of the *Surly* cookbook (`Z:\repos\Surly\Book`).

**Build tested:** Curl at commit `0afbd10e3b` (2026-11-19 05:04): the binary reports `curl 8.22.0`, with `curl-mcp`, `Curl.Api 0.2.0` and `Libcurl 0.2.0` built from it. Windows 11, .NET SDK 11.0.100. The server was Surl at `993aa7072`. Finding 1 was reproduced again just before this report was written.

## Earlier reports: status on this build
- `curl-mcp` `-o NUL`: works (your commit `0276be8194`). `-o /dev/null` exits 23 on Windows, as upstream `curl.exe` does. Resolved.
- `Curl.Api` README gaps (`Curl.Api.Codes`, `Curl.Api.Options.Authentication`, no `-k` sample): not rechecked; writers hit them again.

The Curl port passed every case Part 4 gave it: HTTP/2 negotiated during the TLS handshake (ALPN), the h2c upgrade, HTTP/3 over QUIC on Windows, `--rate`, `-Z --parallel-immediate`, `--retry`, `--max-filesize`, RTSP `OPTIONS` with Digest on one connection, and SCP/SFTP against weak-algorithm and certificate-serving hosts.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Console, HTTP/3 | `--alt-svc` stores a server's `h3` entry but never uses it |
| 2 | Medium | `Libcurl` | `SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `BadFunctionArgument`, so a WebSocket port silently downloads instead |
| 3 | Low | `Libcurl` | Porting friction: namespaces, boxed `GetInfo`, option-name casing |
| 4 | Low | Console | The port can't verify an SSH host certificate (`@cert-authority`) or narrow its SSH algorithms |

---

### 1. `--alt-svc`: the `h3` entry is stored, never used
**Repro:** `surl --directory site --self-signed --http3 https://127.0.0.1:8443/` (it answers with `Alt-Svc: h3=...`), then:
```
curl -s -k --alt-svc altsvc.txt -o NUL -w "%{http_version}" https://127.0.0.1:8443/hello.txt   # 2
type altsvc.txt          # h2 127.0.0.1 8443 h3 127.0.0.1 8443 "20261120 13:42:59" 0 0
curl -s -k --alt-svc altsvc.txt -o NUL -w "%{http_version}" https://127.0.0.1:8443/hello.txt   # 2 again
```
**Expected:** the second request goes over HTTP/3, as curl's alt-svc documentation describes for a build with HTTP3 (this build lists HTTP3 in `curl -V`, and `--http3-only` against the same server works). The same happens with a second URL in one command, and with `--http2` or `--http1.1` given. Upstream couldn't be compared on this machine: Windows' own `curl.exe` has no HTTP/3.

### 2. `Libcurl`: `ConnectOnly.WebSocket` is refused by `SetOption`
`SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `BadFunctionArgument`. `SetOption(CurlOption.ConnectOnly, 2L)` works. `Libcurl` ships the `ConnectOnly` enum, but this option is a `CURLOT_LONG`, which takes only `int` or `long`, while `CURLOT_VALUES` options such as `RtspRequest` do take their enum. C code rarely checks `curl_easy_setopt`'s result, so a call-for-call port ignores it. The transfer then performs a full download instead of the upgrade, and the program hangs waiting for frames. Please accept the enum for `ConnectOnly`, or remove it from the public API.

### 3. `Libcurl` porting friction
- `CurlCode`, `CurlWebSocketFlags`, `RtspRequest` and the callback delegates are in `Curl.Api.*` namespaces, not `Libcurl.*`, so a port needs four or five unexpected `using` lines.
- `GetInfo` returns `out object?`, so every value has to be unboxed (`(long)code!`). Typed overloads, or `GetInfo<T>`, would match how C reads `curl_easy_getinfo` into a typed variable.
- Option names are capitalised oddly (`Writefunction`, `Interleavefunction`), so a reader searching for `WriteFunction` finds nothing.
- `curl_ws_recv` returns `Again` with no socket to wait on, so a port has to poll with a sleep.

### 4. SSH limits of the port (for the record)
- Host certificates: the port's libssh2 1.11.1 prefers the plain host key, so it never receives a certificate, and it ignores `@cert-authority` lines (exit 60). A server serving `--hostcert` can be pinned only by its plain key.
- The port has no way to narrow its SSH algorithms, for example to act as an old client. The cookbook uses OpenSSH `sftp` for both of these.
