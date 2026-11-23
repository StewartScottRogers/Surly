<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (batch 13 of the 1,000-recipe plan)

From writing and verifying batch 13 (60 recipes: mail traces and attachments, proxies and tunnels, libcurl ports, diagnosis from an agent) of the *Surly* cookbook against Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Libcurl progress | The `CURLOPT_XFERINFOFUNCTION` callback is not called while the server holds a transfer; upstream libcurl calls it about once a second, so a progress-callback abort waits for the first data |
| 2 | Low | Libcurl info | `CURLINFO_TOTAL_TIME_T` read inside the progress callback is 0; upstream gives the time elapsed so far |
| 3 | Medium | Curl.Api SMTP | With `MailRecipientAllowFails`, there is no way to see each RCPT reply: the response is 250 and names nobody, and Curl.Api has no debug callback |
| 4 | Low | Curl.Api MIME | A `CurlMimePart.Bytes` part with no `ContentType` is sent with no `Content-Type` header; curl's `-F name=@file` part gets `application/octet-stream` |

## Repros

**1, 2.** `surl --allow-anonymous --directory site --fault "delay;stage=command;command=RETR;path=/slow.bin;ms=3000" ftp://127.0.0.1:2121/`.
- Libcurl port: `CURLOPT_URL ftp://127.0.0.1:2121/slow.bin`, `CURLOPT_NOPROGRESS 0`, an `XFERINFOFUNCTION` that logs each call and reads `CURLINFO_TOTAL_TIME_T`.
- Observed: no call during the 3 s hold; the first call comes with the first data; the time read is 0.
- Expected (upstream 8.22.0): calls about once a second during the hold, with the elapsed time.
- Recipe `ftp-abort-from-a-progress-callback` (case `first-call-with-data`).

**3.** `surl --mail-domain example.com --mailbox ann@example.com:pw smtp://127.0.0.1:2525/`.
- Curl.Api send to `ann@example.com` and `nobody@example.com` with `MailRecipientAllowFails = true`.
- Observed: status 250; nothing says `nobody` was refused 550.
- Expected: each RCPT reply exposed, as `curl -v --mail-rcpt-allowfails` prints them.
- Recipe `smtp-send-despite-a-bad-recipient-in-csharp` (sends one request per address instead).

**4.** Curl.Api `CurlMimePart.Bytes("report", bytes)` with no `ContentType`, sent over SMTP or HTTP.
- Observed: the part has no `Content-Type` line.
- Expected: `application/octet-stream`, as `curl -F report=@report.bin` sends.
- Recipe `smtp-send-mail-with-an-attachment-in-csharp`.
