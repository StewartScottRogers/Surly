<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-19 06:42 (UTC-7)

These findings come from rewriting the MCP recipes and writing Part 4 (Advanced) of the *Surly* cookbook (`Z:\repos\Surly\Book`).

**Build tested:** Surl at commit `993aa7072` (2026-11-19 05:05): `surl 1.0.0 (win-x64)`, plus `surl-mcp` and `Surl.Api` built from it. Windows 11, .NET SDK 11.0.100. The client was the Curl port at `0afbd10e3b`. Finding 1 was reproduced again just before this report was written.

## Earlier reports: status on this build

| Report | Finding | Status |
|---|---|---|
| 1734 #1 | Every MQTT publish stored as retained | **Fixed**: RETAIN 0 is no longer kept; `--topic ...;retain` keeps a topic's last message. Recipes updated. |
| 1734 #4 | `--share` could write into a folder another `surl` holds | **Fixed**: exit 124 now covers the folder of a writable `--share`. |
| 1734 #5 | `put_file` with a bad path was a JSON-RPC internal error | **Fixed**: now a tool error (`NotRooted`, `DotSegment`). The message still ends in .NET's `(Parameter 'requestPath')`, which an agent can't act on. |
| 1734 #2 | Server sends MQTT DISCONNECT | Still open |
| 1734 #3 | `--topic "shop/#/orders"` accepted | Still open: it starts and serves |
| 1233 #1 | `-s` does not silence exit 1 | Fixed earlier; `--aihelp exit-codes` now says so too |

## New findings

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | HTTP / docs | With no accounts, POST, DELETE and PATCH get `401`, but `--aihelp http` says `405` with `Allow: GET, HEAD` |
| 2 | Question | Limits | `--idle-timeout` does not close a connection while the server itself is delaying (a `delay` fault) |
| 3 | Low | Faults | An accept-stage fault with `times=N` is used up by any TCP port check |

---

### 1. Method refusal: `401` or `405`?
**Repro**
```
surl --directory site http://127.0.0.1:8080/
curl -s -o NUL -w "%{http_code}" -X POST -d x http://127.0.0.1:8080/hello.txt     # 401
curl -s -D - -o NUL -X DELETE http://127.0.0.1:8080/hello.txt                       # 401, WWW-Authenticate: Digest ...
```
**Observed:** `401 Unauthorized` with Digest challenges.
**Documented:** `surl --aihelp http` says: "POST, PUT, DELETE, CONNECT, OPTIONS, TRACE and PATCH are answered 405 Method Not Allowed with `Allow: GET, HEAD`; under `--...`".
**Expected:** one of them should change. If the login check is meant to come before the method check (secure by default: an unauthenticated client learns nothing), fix the help. If `405` is meant, the server is wrong. The cookbook's `http-serve-an-empty-store` claims `401` for DELETE and will follow whichever you choose.

### 2. `--idle-timeout` and a server-side delay (question)
`surl --directory site --idle-timeout 1 --fault "delay;stage=command;ms=3000" http://127.0.0.1:8080/`, then `curl http://127.0.0.1:8080/hello.txt`, gets `200` after about 3.7 s, though no byte moved for 3 s. ADR-0006 defines idle as "no byte moves in either direction", which would close it. This is probably intended (the client is the one waiting, on the server), but the help doesn't say which side's silence counts.

### 3. Accept-stage `times=N` and port checks
A fault rule at the accept stage with `times=N` is used up by any TCP connection that opens and closes without a request, such as a readiness probe or a load-balancer health check. A test that starts `surl`, waits for the port and then expects the fault on its first real request finds the fault already spent. Consider counting only connections that send a byte, or saying in the help that port checks count.
