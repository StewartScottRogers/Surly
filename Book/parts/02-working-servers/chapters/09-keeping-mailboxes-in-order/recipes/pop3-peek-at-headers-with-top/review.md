<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-peek-at-headers-with-top
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. Seed waits with `curl --retry ... -X "LIST 3"` (exit 8 while message 3 is missing).
2. `--next` POP3 transfers share one session, so DELE then UIDL in one curl still shows the old numbering; the case adds a second curl (&&) for the new session and claims both.
3. `curl -X TOP pop3://h/2%200` sends `TOP 2 0` (path appended, decoded); the C# twin relies on it since Curl.Api methods are one token.
4. surl answers `TOP 1` (no line count) with +OK, though RFC 1939 requires both arguments; not claimed. Program finding, minor.
