<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-record-what-the-client-sent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Fixed: removed the 1 MiB truncation bound and "reading Exchanges waits for published events" from the Discussion (README facts, no case); the prose now says only what the program observed.
2. Fixed: removed the comparison with the `curl` command line's User-Agent; this recipe has no console case for it.
3. Machine (rubric 12): case no-user-agent is a negative-lookahead regex, which reads poorly in the claims table. A schema `stdout-lacks:` would say it plainly; left for the author.
4. Notable for readers: an exchange is a connection, not a request (case one-connection-one-exchange). A test that counts `Exchanges` to count requests will be wrong whenever the client reuses a connection.
5. Cross-reference <<http-wait-for-an-upload-in-process>> points at a recipe not written yet (chapter 10).
6. Same package-feed note as http-serve-and-fetch-in-process. No fixtures, so the CRLF fix does not affect it.
