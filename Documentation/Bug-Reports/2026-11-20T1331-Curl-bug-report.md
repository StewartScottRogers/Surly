<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-20 (re-pin to Curl 7d167fa10a)

From re-verifying all 343 recipes of the *Surly* cookbook against Curl `7d167fa10a` (work/dark-factory) and Surl `9ad60d80c`. The fixes in this build (BL-5594, BL-5603, the curl-mcp `-o` and `caFile` changes, S3 and Azure query strings, Kafka SCRAM) are now used by the book; thank you. Two findings:

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api FTP | With `FetchFileTime = true`, a `HEAD` on an `ftp://` URL leaves `CurlResponse.Info.FileTime` at -1, though the README says it holds the file time; the `Last-Modified` header does carry it |
| 2 | Check | Curl.Api start-up | The first request through Curl.Api in a fresh process takes about 4 s on an idle Windows 11 machine (later requests are milliseconds). Recipes with short timers had to make an untimed request first. Is that expected (native load, TLS init)? |
