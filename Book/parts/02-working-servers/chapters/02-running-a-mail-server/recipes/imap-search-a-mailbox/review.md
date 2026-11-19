<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-search-a-mailbox
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 14 of 14 cases pass on windows.
## Findings
1. Needs the author: run.sh/run.ps1 run only the Solution, against an empty store, so a reader who runs them sees a bare `* SEARCH`. The prose says the claims deliver three messages first. Folding three SMTP sends into the Solution would make it unreadable; a way to seed the store from fixtures (or a setup role for cases) would fix it in the schema.
2. Fixed: the bad-criterion claim now uses `-v` and checks the `BAD` reply, since the Discussion names it.
3. Truth (5): "message numbers shift, UIDs do not" is cased in <<imap-append-and-flag-a-message>> (after an expunge) and pointed at; here the case only shows they coincide.
4. The unquoted `SUBJECT invoice 42` finding nothing is correct IMAP (42 is parsed as a sequence-set criterion, <<rfc9051>>) and is kept as a claim because it is the likeliest mistake a reader makes.
