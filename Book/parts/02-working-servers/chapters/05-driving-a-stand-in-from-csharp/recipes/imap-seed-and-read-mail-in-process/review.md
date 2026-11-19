<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-seed-and-read-mail-in-process
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. For the author (key): the key starts with `smtp`, but no SMTP is spoken. `Mail.DeliverAsync` needs no SMTP listener, and the server listens on IMAP only, so `protocols` is `[imap]`. The title is "Seeding a Mailbox and Reading It over IMAP from C#". The key is fixed, so it stays.
2. Fixed: removed three claims that had no case: that a seed "is stored the way SMTP delivery stores it", that `curl` on the console exits 67 too, and that listing the store sets no flag.
3. Fixed: the login mechanism (CRAM-MD5, since surl offers LOGIN and PLAIN over `imap://` only with `--allow-plaintext-auth`) is not named. No case shows it. The Discussion says only that the login works with no loosening, which the solution proves.
4. For the author (API docs): the Surl.Api README says flags are IMAP names "(`Seen`)". This build returns `\Seen`, with the backslash, as RFC 9051 spells it. The recipe follows the build.
5. Reader friction: `CurlException` is in `Curl.Api.Errors`, not `Curl.Api`. Over IMAP `CurlResponse.StatusCode` is 0, so the program does not print it.
6. Cross-reference <<smtp-seed-mail-from-an-agent>> points at a recipe not written yet (chapter 11).
