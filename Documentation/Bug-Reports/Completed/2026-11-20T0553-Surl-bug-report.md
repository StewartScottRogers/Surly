<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-20 (batch 3 of the 1,000-recipe plan)

From writing and verifying batch 3 (62 recipes: retries and faults, proxies, HTTPS moves, checksums, object stores, CI and diagnosis, TFTP, Telnet) of the *Surly* cookbook. Follows `2026-11-19T2203-Surl-bug-report.md`; earlier items still stand.

**Build tested:** Surl at commit `763754785`: `surl 1.0.0`, `surl-mcp`, `Surl.Api 1.0.0`. Windows 11. Client: the Curl port at `b9ca725077`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | **High** | Logging | `surl -v` (and `--trace-ascii`) writes secrets in clear: FTP `PASS <password>`, also over FTPS, and HTTP `Authorization: Basic` and `Bearer` values. A log kept from a failed CI run leaks them |
| 2 | Medium | Logging | `--log-file f --trace-ascii t`: the exchange lines go only to `t`, not to `f` |
| 3 | Low | `surl-mcp` | `start_server` with `--log-file` is refused with a message naming `SurlServerOptions.Log`, which an MCP caller has never seen; it should name the tool's own field |

### 1. Secrets in the log
`surl -v --user alice:s3cret ftps://127.0.0.1:PORT/` and `curl -k -u alice:s3cret ftps://127.0.0.1:PORT/`: the log shows `PASS s3cret`. Over HTTP, `-v` shows the `Authorization` header value. **Expected:** the password and token values redacted (for example `PASS ****`, `Authorization: Basic ****`), so a log can be kept. The cookbook's keep-the-log recipes filter these lines out before keeping a log, and say why.
