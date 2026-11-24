<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-22 (batch 13 of the 1,000-recipe plan)

From writing and verifying batch 13 (60 recipes) of the *Surly* cookbook against Surl v0.1.5 `d64faa4bb`, Curl `2145a49eae`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Question | Faults | `--fault "drop;after-bytes=N"` resets the connection rather than closing it. curl exits 56 `Recv failure`, never 18 (partial file), and the reset discards bytes the client had not read, so the amount kept varies with load. Intended? A clean close would let a test show a cut-off download as curl sees one from a real server |
| 2 | Question | Hooks | An override that returns an invalid fault (`SurlFault.ProtocolError("404") with { Stage = FaultStage.Command }`) publishes `HookFailed` (`option --fault: key stage does not apply to error`) and the request is served 200. A test that misses the event passes against the wrong answer. Should an invalid override fail the request (500), or be refused when set? |
| 3 | Low | SOCKS5 log | The `-v` log masks the SOCKS5 password as `****` but keeps the RFC 1929 length byte before it, so the password's length shows |

## Repros

**1.** `surl --directory site --fault "drop;after-bytes=4000000" http://127.0.0.1:8080/`, `site/big.txt` 5,000,001 bytes.
- `curl -o big.txt http://127.0.0.1:8080/big.txt` → exit 56; between 1 and 4 MB kept (204,603 of 500,000 seen once at a smaller scale).
- Expected, if a close: exit 18 and exactly 4,000,000 bytes kept.
- Recipe `http-cut-a-download-off-halfway`.

**2.** Surl.Api: an override on an http server returning `SurlFault.ProtocolError("404") with { Stage = FaultStage.Command }`; `curl http://127.0.0.1:{port}/a.txt` → 200 with the file; the event stream holds `HookFailed`. Without `Stage`, 404 is answered.

**3.** `surl socks5h://127.0.0.1:1080/ -U app:socks-pw --allow-plaintext-auth -v --log-file open.log` beside `surl --directory site http://localhost:8080/`; `curl --proxy socks5h://app:socks-pw@127.0.0.1:1080 http://localhost:8080/` → `open.log` shows the login with the length byte `\x07` before `****`: the password's length (7). Recipe `socks5-require-a-socks-login`.
