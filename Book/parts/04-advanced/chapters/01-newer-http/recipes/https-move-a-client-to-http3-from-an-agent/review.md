<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-move-a-client-to-http3-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 windows cases pass; the [linux, macos] case is skipped here.
## Findings
1. Same platform split as the console twin; curl-mcp's curl follows ADR-1313 on Windows.
2. `transfer` has neither an alt-svc nor a version field, so both calls use the `curl` tool; the program reads the alt-svc file from the shared root.
3. QUIC is shown by `get_exchanges` chunks carrying `stream_id`.
4. Revised after the Curl session (ADR-1313 stands; with --http3 a stored h3 entry is used on Windows too): the solution passes --http3 (Http3) and runs on all three platforms; the no-version-option switch is a [linux, macos] case and the stay on HTTP/2 a [windows] case.
5. Bounded wait before reading the server's record (CI race sweep).
