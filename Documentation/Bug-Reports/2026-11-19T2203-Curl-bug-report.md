<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-19 (batch 2 of the 1,000-recipe plan)

From writing and verifying batch 2 (61 recipes) of the *Surly* cookbook and from fixing 11 recipes for Linux and macOS in CI. Follows `2026-11-19T1942-Curl-bug-report.md`, whose items still stand (its #2, SMTP from a byte body, is one case of #1 below).

**Build tested:** the Curl port at `b9ca725077`: `curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`. Windows 11; Linux and macOS through CI. Server: Surl `763754785`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | **High** | Curl.Api uploads | A `PUT` or `POST` with a body to `ftp://` or `smb://` is sent as a download (FTP: `RETR`, status 0, file unchanged; SMB: exit 78). Only HTTP and SMTP-with-a-stream upload. `curl -T` works for all |
| 2 | Medium | CoAP (Linux, macOS) | `curl -s coap://127.0.0.1:<unbound>/hello.txt` exits 7 at once on Windows, but on Linux and macOS was still running at the 60 s limit |
| 3 | Medium | Libcurl SMB | `Upload = 1L` with a read callback and `Infilesize` or `InfilesizeLarge` (either order) stops with 55, "SMB upload needs to know the size up front" |
| 4 | Medium | Write-out | `curl -w '%{filetime}'` is refused: `unknown --write-out variable: 'filetime'`. Upstream curl has it |
| 5 | Low | Curl.Api FTP | No `FILETIME` option; a `HEAD` on an FTP URL sends `SIZE` only, so the date needs `MDTM` through `Quote` |
| 6 | Low | `curl-mcp` | `-o "reports/report-#1.csv"` with a glob is refused `PathRefused` before curl runs; `-O --output-dir reports` works |
| 7 | Low | `curl-mcp` `transfer` | An empty `Accept` value sends `Accept:` empty, where `curl -H "Accept:"` drops the header |
| 8 | Check | Retry | `curl -sS --retry 1 --retry-all-errors` against a dropped connection exits 0 but prints the first attempt's `curl: (56) Recv failure`. Please compare with upstream |
| 9 | Check | LDAP (Linux, macOS) | With the OpenLDAP backend, `--ntlm` exits 67 even against a server that accepts NTLM; other bind refusals are 38 (`LDAP: cannot bind`). Please confirm these match the reference builds |
