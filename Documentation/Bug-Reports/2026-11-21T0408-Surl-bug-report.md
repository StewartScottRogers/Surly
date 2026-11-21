<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-21 (batch 8 of the 1,000-recipe plan)

From writing and verifying batch 8 (61 recipes: retries and faults, mail faults, proxies, FTP folders, Libcurl ports, Gopher, seeding, MQTT) of the *Surly* cookbook against Surl `9ad60d80c`, Curl `7d167fa10a`, Windows 11. Only findings not already filed are listed; some may be fixed on master.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Faults | A wrong password is refused before a `refuse-login` rule is checked, so the refusal is not the rule's and the rule stays unspent |
| 2 | Medium | Faults | After a `delay` rule's hold ends, surl still sends `200 OK` to a client that already timed out, so the exchange record looks served |
| 3 | Medium | Surl.Api | `StartAsync` with only `--proxy` listeners in `AdditionalArguments` throws `ListenUrls names no listen URL`; `surl --proxy ...` alone starts |
| 4 | Low | Faults | `list_faults` and `SurlFaultRule.Item` give a rule back percent-encoded and without `times` |
| 5 | Low | Proxy | `--proxy-auth digest` (a narrowing, no loosening) prints a warning |
| 6 | Low | Events | `StoreChangedEvent.Account` is empty for a mail delivery |

## Repros

Each repro is the command shape from the cookbook recipe named in its review; ports and file names are examples.


**1.** `surl --user alice:s3cret --fault "refuse-login;times=1" ftp://127.0.0.1:2121/`, then `curl -u alice:wrong ftp://127.0.0.1:2121/` (exit 67), then `curl -u alice:s3cret ftp://127.0.0.1:2121/`. Observed: the second login is refused by the rule (it was not spent by the wrong password). Expected: either the first refusal counts as the rule's, or the docs say a wrong password does not spend a rule.

**2.** `surl --directory site --fault "delay;path=/report.txt;ms=3000" http://127.0.0.1:8080/` and `curl -s -m 1 http://127.0.0.1:8080/report.txt` (exit 28). Observed on Windows: the exchange record (`--trace-ascii`, `get_exchanges`) shows `200 OK` sent about 3 s later. Expected: the record shows the client gone, nothing sent.

**3.** `new SurlServerOptions { AdditionalArguments = { "--proxy", "http://127.0.0.1:0/" } }`, then `SurlServer.StartAsync`. Observed: `ArgumentException: ListenUrls names no listen URL`. Expected: it starts, as `surl --proxy http://127.0.0.1:3128/` does.

**4.** `surl --fault "error;scheme=http;path=/report.txt;reply=503;times=2" ...`, then `list_faults` (surl-mcp) or `server.Faults.List()` (Surl.Api). Observed: `error;scheme=http;path=%2Freport.txt;reply=503`. Expected: the rule as written, with `times=2`.

**5.** `surl --proxy http://127.0.0.1:3128/ --proxy-user p:q --proxy-auth digest`. Observed: a warning line at start. Expected: no warning; Digest-only is stricter than the default.

**6.** `Surl.Api` server with SMTP and a mailbox; subscribe to `OnEvent`; `curl --mail-rcpt ann@example.com -T mail.eml smtp://...`. Observed: the `StoreChangedEvent` for the delivery has `Account = ""`. Expected: `ann@example.com`.
