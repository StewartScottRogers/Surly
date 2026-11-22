<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-which-listener-was-used-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. `SurlExchangeRecord.ListenUrl` and `SurlEventQuery.ListenUrl` are `Uri`; `WaitForExchangeAsync` with Kind ExchangeEnded is the bounded wait. A `Host` in `Headers` replaces the default one (one Host sent).
