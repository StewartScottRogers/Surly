<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-which-client-sent-bad-requests
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 6 of 6 cases pass on windows.
## Findings
1. "Recorded endpoints" is read as the listen URL: one server with three http listeners, one per client; exchanges carry listen_url and get_exchanges filters by it.
2. The client-address claim is "2 to 4 different" for 4 connections, not exact (source ports are the system's).
3. A 404 is shown not to count as a bad request; each request is its own curl call and exchange.
