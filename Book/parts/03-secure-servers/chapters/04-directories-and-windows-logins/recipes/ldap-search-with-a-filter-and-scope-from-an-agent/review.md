<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-search-with-a-filter-and-scope-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 8 of 8 cases pass on windows.
## Findings
1. One program, two servers, ten searches; the cases match lines of its output. `add_ldap_entries` answers `{"entry_count":6}`.
2. The error wording is the platform's (Windows `LDAP remote: No Such Object`, `bind via ldap_win_bind Inappropriate Authentication`), so the program prints curl's exit code, 39 and 38, instead; the claims do not depend on the platform.
3. curl-mcp's `transfer` tool has no LDAP-specific option and refuses nothing here, but the anonymous bind needs `-u :`, so the `curl` tool is used.
