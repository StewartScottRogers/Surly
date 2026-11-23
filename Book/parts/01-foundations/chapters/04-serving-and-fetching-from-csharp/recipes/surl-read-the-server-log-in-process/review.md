<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-read-the-server-log-in-process
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 5 of 5 cases pass on windows.
## Findings
1. Log = TextWriter.Synchronized(StringWriter); read after the server stops so the exchange-ended lines are in.
2. AllowAnonymous is there to give the log a warning and let the test fetch with no login; the strict case shows no warning and 401.
3. Observed: LogLevel.Error writes nothing, not even the start-up warning (the Accounts.Remove XML doc says warnings show 'at every log level that shows warnings'; consistent, Error is errors only).
4. The 404 path differs by platform; the pattern matches any path ending summary.csv.
Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: the Exchange opened line names the listener; solution and info-level match it against the Listening on URL. 5 of 5 pass on windows.
