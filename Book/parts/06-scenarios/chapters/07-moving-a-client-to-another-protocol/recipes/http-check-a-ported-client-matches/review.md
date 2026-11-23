<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-check-a-ported-client-matches
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 5 cases pass on windows (verify.py --no-write).
## Findings
1. transfer has no login, referrer or compression field, so a port of -u, -e and --compressed loses three headers; the agent copies them from the old request's recorded head.
2. transfer and curl -d both send Content-Type: application/x-www-form-urlencoded and a matching Content-Length; the comparison ignores Host only.
3. Each request is its own exchange; heads are read by exchange_id after a bounded wait for exchange_ended.
