<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-expire-a-key

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.

## Findings
1. The solution is a short sh script (a fixture shown with the commands); its Solution says it needs a POSIX shell (Git Bash on Windows). The wait is three seconds against a two-second life; PX 2000 and the three-second sleep leave one second either side. A slow machine could make the first run exceed two seconds only before its second URL; both reads happen in one curl run.
2. Truth: WRONGPASS for a clear-text password without --allow-plaintext-auth is kept on purpose (Surl ADR-0106), shown as a case.
