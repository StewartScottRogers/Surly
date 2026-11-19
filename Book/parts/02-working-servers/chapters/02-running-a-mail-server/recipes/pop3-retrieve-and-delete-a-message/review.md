<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-retrieve-and-delete-a-message
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 13 of 13 cases pass on windows.
## Findings
1. Fixed: the first draft's Solution assumed mail delivered by earlier cases, so run.sh exited 8 on an empty maildrop. The Solution now sends the message itself, then retrieves and deletes it, in one `curl` with `--next`.
2. Truth (4): that `--next` keeps one POP3 session is shown by `dele-then-rset` (message 2 keeps its number in-session; both messages survive `RSET`). That deletions are committed at `QUIT` is cited to <<rfc1939>>; probed by hand with a raw socket: `DELE 1` then disconnect without `QUIT` deletes nothing, with `QUIT` it deletes. Not a case: the schema has no raw-socket client.
3. Truth (5): "the unique id is what a client keeps" is cited to <<rfc1939>>; the cases show UIDL's shape, not that an id survives renumbering across sessions (not checkable in one case without a fresh connection inside one curl).
4. Reader: `--allow-anonymous` refusal is pointed at <<smtp-send-and-read-it-over-imap>>, where it is cased, rather than repeated with a second server.
5. Checked against upstream curl 8.22.0 (Windows build) as a yardstick: `-X "DELE 1"` without `-I`, `DELE`/`LIST`/`RSET` over `--next`, and the empty-list output behave the same.
