<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-replay-a-request-from-a-trace
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 9 of 9 cases pass on windows.
## Findings
1. The replay reads `surl --trace` (hex), not the -v log, so bytes are exact; request-of.sh is awk only (no xxd) and needs a POSIX shell.
2. `curl -T f telnet://h:p` sent the request unchanged (cmp identical); a 400 closes the connection, and `--idle-timeout 2` ends a replay that would stay open; every replay has -m 10.
3. The trace withholds a Digest response as `****`, so a login replays as two 401s; stated, with RFC 7616 cited for why a Digest response would not replay anyway.
4. Linux and macOS not yet run in CI (awk, cmp, sed under sh).
