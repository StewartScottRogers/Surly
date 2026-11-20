<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-parse-a-url-before-fetching-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. The URL is the program's argument with PORT standing for the bound port, as a user would type it.
2. GuessScheme and NonSupportScheme flags were probed (they accept the no-scheme and htp URLs) but are not claimed; recorded in facts/http.md.
