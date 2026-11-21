<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-20 (batch 7 of the 1,000-recipe plan)

From writing and verifying batch 7 (62 recipes: FTP faults and resume, mail aliases and restarts, HTTP/2 and HTTP/3, parallel and conditional downloads, SFTP ciphers, TFTP uploads, config files) of the *Surly* cookbook against Surl `9ad60d80c`, Curl `7d167fa10a`, Windows 11. Only findings not already filed are listed; some may be fixed on master already.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Faults | `--fault "drop;after-bytes=N"` on FTP counts only control-connection bytes, so a download cannot be cut partway through the file (a resume test needs that) |
| 2 | Medium | `surl-mcp` | `start_server` given about 5 MB of `files` (50 files of 100,000 characters) never returns; 50 of 50,000 works. Expected an answer, or a refusal naming a size limit |
| 3 | Low | Faults | A client that cancels a request held by a `delay` fault still holds its slot until the delay ends, so with `MaxConnectionsPerAddress = 1` the next request gets 503 |
| 4 | Low | Faults | `out-of-space` on HTTP answers 413; 507 Insufficient Storage would match the fault's meaning. On FTP the log line ("The upload grew past the limit") does not name the fault rule |
| 5 | Low | Surl.Api | `SurlServerOptions` properties such as `MaxFileSize` are applied before `ConfigFiles` and lose to them, while `AdditionalArguments` win. The order is not documented |
| 6 | Low | Shutdown | After a clean stop surl leaves its lock file in the data directory (a later start ignores it) |
| 7 | Low | Files | `Files.DeleteAsync` on the only file in a folder leaves the empty folder; an alias refusal ("alias is also a mailbox") lacks the `(2)` prefix the other two alias refusals have |
