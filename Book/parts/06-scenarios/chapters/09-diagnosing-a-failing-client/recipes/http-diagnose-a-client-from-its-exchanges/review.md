<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-diagnose-a-client-from-its-exchanges
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 5 of 5 cases pass on windows.
## Findings
1. Distinct from http-compare-a-good-and-a-bad-request-from-an-agent: no good request to diff; the agent checks each recorded header line against RFC 9110 field rules (token name, colon, Content-Length digits and agreement).
2. Three 400 causes probed in surl's -v log first: MalformedHeaderField (space in name), "the Content-Length is not one field of decimal digits" for both `ten` and two differing values; the recipe does not quote surl's reasons, since surl-mcp has no log.
3. Each user's request is its own curl call, so its own exchange, well under get_exchanges' 64 KiB cap.
