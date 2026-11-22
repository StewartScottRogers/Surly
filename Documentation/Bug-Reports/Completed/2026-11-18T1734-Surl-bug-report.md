<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-18 17:34 (UTC-7)

These findings come from writing and verifying Part 2 (Working Servers) of the *Surly* cookbook (`Z:\repos\Surly\Book`). Unless a finding says otherwise, each was reproduced again just before this report was written.

**Build tested:** Surl at commit `a0236e175` (2026-11-18 13:15), from `dotnet build -c Release` of `Application/Surl.Console` and `Application/Surl.Mcp`. `Surl.Api` was packed with `-p:BundledProjectReferencesPrivateAssets=all`. The binary reports `surl 1.0.0 (win-x64)`. Windows 11, .NET SDK 11.0.100. The client was the Curl port at `f4b30d981f`, and upstream `curl.exe` 8.22.0 (Windows) was used for comparison.

## Earlier report (`2026-11-18T1233-Surl-bug-report.md`): status on this build

| # | Finding | Status |
|---|---|---|
| 1 | `-s` does not silence the exit-1 message | **Fixed**: `surl -s nosuch://127.0.0.1:0/` now writes nothing and exits 1 |
| 2 | DICT DEFINE and MATCH disagree on case | **Fixed**: `d:SURLY` and `m:SURLY:*:exact` both answer `552 no match` for headword `surly` |
| 3 | `Surl.Api` README gaps (`SurlExitCode` namespace, `(58) ` prefix) | Not rechecked |

## New findings

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | MQTT server | Every PUBLISH is stored as retained, even with RETAIN 0 |
| 2 | Low | MQTT server | The server sends DISCONNECT to a subscriber; in MQTT 3.1.1 only a client sends it |
| 3 | Medium | Options | `--topic` accepts an invalid topic filter (`shop/#/orders`) |
| 4 | Medium | Content store | `--share` can write into a folder another `surl` holds with `--directory` |
| 5 | Low | `surl-mcp` | A relative or `..` path to `put_file` gives JSON-RPC `-32603 Internal error`, not a tool error |
| 6 | Low | Docs | README and `Surl.Api` README mismatches (four items) |
| 7 | Cosmetic | Logging | A connect-and-close (a port check) is logged as an `IOException` |

---

### 1. MQTT: every publish is retained
**Repro**
```
surl --allow-anonymous mqtt://127.0.0.1:1883/
curl -s -d 22.0 mqtt://127.0.0.1:1883/sensors/3        # publish; first byte 0x30, so RETAIN 0
curl -s mqtt://127.0.0.1:1883/sensors/3                # a later subscriber
```
**Observed:** the later subscriber receives the message: stdout `\x00\x09sensors/3` + `22.0`. `surl-mcp`'s `get_exchanges` shows the PUBLISH fixed header `0x30`, and `read_retained_message` then answers `{"topic":"sensors/3","qos":0,"text":"22.0"}`.
**Expected:** MQTT 3.1.1 §3.3.1.3 says that when RETAIN is 0 the server must not store the message as retained, nor remove or replace an existing retained message. `surl --aihelp mqtt` documents the current behaviour, so this deviation is deliberate. It still breaks clients that rely on the specification, and it makes `--topic ...;retain` impossible to observe over MQTT. Three Part 2 writers found this independently.
**Note:** the curl tool cannot set RETAIN 1, so a fix needs another way for curl users to leave a retained message. The *Surly* recipes depend on the current behaviour and will need rewriting if it changes.

### 2. MQTT: the server sends DISCONNECT
**Repro:** same server as finding 1, then `curl -v mqtt://127.0.0.1:1883/sensors/3`.
**Observed:** after the retained messages, `curl -v` shows `< ... Got DISCONNECT` (a packet starting `0xE0`).
**Expected:** in MQTT 3.1.1 (§3.14) DISCONNECT goes from client to server only. To end a session the server should close the network connection. Sending DISCONNECT is MQTT 5 behaviour.

### 3. `--topic` accepts an invalid topic filter
**Repro:** `surl --user ann:secret --topic "shop/#/orders" mqtt://127.0.0.1:0/`
**Observed:** it starts and listens until stopped. Under `timeout 4` it was still running.
**Expected:** `#` is valid only as the last level, so the command should exit 2 with "not a valid topic filter". `surl --aihelp messages` says it does.

### 4. `--share` can write into a folder another `surl` holds
**Repro** (reported by the chapter writer on this build; not re-run for this report):
```
surl --directory site --allow-uploads --allow-anonymous http://127.0.0.1:8080/
surl --share keep=site --allow-uploads --allow-anonymous http://127.0.0.1:8081/
curl -T up.txt http://127.0.0.1:8081/keep/viashare.txt     # 201
curl http://127.0.0.1:8080/viashare.txt                    # the first server serves it
```
**Expected:** the second start should be refused with exit 124, as a second `--directory site` is. A share is a path one process writes, so the one-process-per-path rule should cover it.

### 5. `surl-mcp`: `put_file` with a bad path is an internal error
**Repro:** call `start_server` with `{"listen_urls":["http://127.0.0.1:0"]}`, then `put_file` with `{"server_id":"s1","path":"reports/b.txt","text":"x"}` or with `"path":"/../x.txt"`.
**Observed:** JSON-RPC error `{"code": -32603, "message": "Internal error"}`.
**Expected:** a tool result with `isError: true` that names the problem, as other tools do for bad input (`no file at <path>` and similar). By comparison, a wrongly typed `get_events` argument now gets a clear `-32602 ... expected an integer`.

### 6. Documentation
- **README, "Configuring a real server":** the example grants share `docs` to `ann@example.com`, a `--mailbox` account. Mailbox accounts log in only on the mail protocols, so over HTTP both her Digest login and an anonymous request get `404`, and nobody can read `docs` over HTTP. Either grant a `--user` account in the example or say that mailbox accounts don't reach shares.
- **`Surl.Api` README, mail flags:** the README says flags are "IMAP names (`Seen`)", but the build returns `\Seen`. The build matches RFC 9051, so the README should change.
- **`Surl.Api` README, namespaces:** `StoreKind` and `StoreChangedEvent` live in `Surl.Protocol.Abstractions`, which the README doesn't name. `WaitForEventAsync` returns the base event type, so reading `Path` needs a cast that the README doesn't show.
- **`Surl.Api`, wait with recording off:** with `RecordExchanges` off, `WaitForEventAsync` throws an `InvalidOperationException` whose message says the setting is needed "to read Exchanges", which is misleading for a wait.

### 7. Port checks logged as errors (cosmetic)
**Repro:** `surl --allow-anonymous smtp://127.0.0.1:2525/ imap://127.0.0.1:1143/`, then open a TCP connection to either port and close it without sending anything.
**Observed:** the log shows `Exchange N ended because the protocol server threw IOException: ... aborted by the software in your host machine`.
**Expected:** a quiet close, or a plain "client closed the connection" line. Every readiness check and load-balancer health check will log this line.

### Question, not a bug
`surl-mcp`'s `seed_mail` matches the account by local part alone, ignoring case and domain: `ALICE@example.com` and `alice@other.org` both deliver to account `alice`. This may be intended, since it is the same rule SMTP delivery uses without `--mail-domain`. If so, documenting it would help.
