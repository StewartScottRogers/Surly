<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (batch 13 CI on macOS)

From batch 13's CI run 45115316914 on macos-latest, Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | macOS, aborted transfer | When the server aborts a connection mid-body (surl's `drop;after-bytes=N`, a reset), curl on macOS sometimes never ends: it waits out `--max-time`. On Windows and Linux it exits 56 `Recv failure` at once. The same was seen earlier for Curl.Api on macOS (body read never ended) |

## Repros

**1.** On macOS: `surl --directory site http://127.0.0.1:8080/ --fault "drop;after-bytes=4000000"`, `site/big.txt` 5,000,001 bytes.
- `curl -sS --max-time 30 -o dl/big.txt http://127.0.0.1:8080/big.txt` → sometimes exit 56 at once; sometimes no end until `--max-time` (exit 28).
- `curl -sS --max-time 30 --retry 2 --retry-all-errors --retry-delay 1 -o dl/again.txt http://127.0.0.1:8080/big.txt` → the run took over 60 s.
- Expected: exit 56 every time, as on Windows and Linux.
- Intermittent: in the same CI run, other cut requests on macOS exited 56 at once. If it is the server's abort not reaching the client as a reset on macOS, it belongs to Surl; please measure which side holds the connection.
- Recipes `http-cut-a-download-off-halfway` (cases `solution`, `restart-never-finishes`) and `-from-an-agent` (`restart-never-finishes`), now marked [windows, linux].
