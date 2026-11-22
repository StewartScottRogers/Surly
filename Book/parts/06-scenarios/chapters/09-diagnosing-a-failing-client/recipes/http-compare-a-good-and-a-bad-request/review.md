<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-compare-a-good-and-a-bad-request
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. The 400 comes from a header name with a space before its colon; surl's -v log names it `WhitespaceBeforeColon` (a name with a space inside is `MalformedHeaderField`; a missing Host has its own sentence). Both claimed from the log, not from RFC 9112, which the bibliography lacks.
2. compare.sh waits (bounded, 10 s) until every opened exchange has ended before it reads the log; diff's normal output (`5c5`, `2d1`) is POSIX, so Linux and macOS should print the same. CI not yet run.
3. verify's readiness probe is exchange 1 in the log; the script finds exchanges by status line, so it does not matter.
