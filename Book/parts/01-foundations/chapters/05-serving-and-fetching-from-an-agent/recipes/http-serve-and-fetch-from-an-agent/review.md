<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-and-fetch-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl-mcp 1.0.0 (Surl commit 3c5b39474), curl-mcp 0.1.0.0 (Curl commit b140f6d88c): 6 of 6 cases pass on windows.
## Findings
1. Fixed: the Discussion claimed that files were never written to disk, that the first ID is s1, and that headers keep the order received. None of these had a case, so I removed them.
2. Fixed: the title was improved to "Serving and Fetching Files from an Agent" (7 words, no acronym). The key is unchanged.
3. Re-verified after the verify.py byte-exact fixtures fix: no claim here depends on fixture bytes (fixtures: {}; content goes in through start_server's files), so nothing changed.
4. For the author: the start_server port-in-use exit code (45, BindFailed) agrees with `surl --aihelp exit-codes`. The chapter list's "path in use (124)" is a different code.
