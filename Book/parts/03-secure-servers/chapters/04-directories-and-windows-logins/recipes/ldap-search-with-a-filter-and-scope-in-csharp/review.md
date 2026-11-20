<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-search-with-a-filter-and-scope-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. One program, two in-process servers; the cases match lines of its output. The directory is seeded with `server.Directory.AddEntriesAsync`, not a file.
2. The program prints entries on one line each so the WinLDAP/OpenLDAP layout difference does not reach the cases; only Windows was run. The error codes 39 and 38 are curl's own, which OpenLDAP shares.
3. `UserPassword = ":"` is how the anonymous bind (curl `-u :`) is spelled in Curl.Api; no member says "anonymous".
