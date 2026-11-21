<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-20 (batch 7 of the 1,000-recipe plan)

From writing and verifying batch 7 (62 recipes) of the *Surly* cookbook against Curl `7d167fa10a` and Surl `9ad60d80c`, Windows 11. Only findings not already filed are listed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | HTTP/3 | `curl --http3-only` against a UDP listener that answers, but not in QUIC, ran 300 s and exited 28; Curl ADR-0144 expects 55 after about 10 s |
| 2 | Medium | Curl.Api HTTP/3 | The first QUIC request in a process lost the race to TCP, and once hung past 60 s; a warm-up `Http3Only` request on a separate client avoids it |
| 3 | Question | Alt-Svc on Windows | Without a version option, curl, Curl.Api and curl-mcp ignore a saved `h3` Alt-Svc entry on Windows (Curl ADR-1313), though this Windows build does speak HTTP/3. Is the ADR still right? |
| 4 | Low | Curl.Api | No resume option for FTP (the C# recipe sends `REST` through `PreQuote` and sets `IgnoreContentLength`); no names-only FTP listing |
| 5 | Low | Curl.Api / curl-mcp SSH | Neither can narrow its own SSH cipher or MAC list (curl's `--ciphers` for SSH) |
| 6 | Check | `--max-filesize` on upload | The exit races between 70 and 55 (Curl.Api: `RemoteDiskFull` and `SendError`) when the server refuses mid-upload |
| 7 | Check | Retry | `--retry-max-time 1 --retry-delay 3 -f` still made one retry; please compare with upstream |
| 8 | Check | TFTP | An empty file cannot be uploaded over TFTP (exit 26); please compare with upstream |
