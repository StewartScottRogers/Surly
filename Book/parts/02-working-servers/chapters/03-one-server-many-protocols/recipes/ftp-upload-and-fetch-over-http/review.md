<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-upload-and-fetch-over-http
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 8 of 8 cases pass on windows.
## Findings
1. Fixed: the 550 and 530 replies were asserted from a hand probe; the two refusal cases now run with `-v` and check `< 550` and `< 530` on standard error.
2. Fixed: "the file stays after surl stops", "surl creates no directory" and "curl exits 0 only if both transfers succeed" had no case; removed. The in-memory store is now a case of its own (a second server without `--directory`), not a citation.
3. Truth (4): two claims rest on <<surl-readme>> and are not cases: an HTTP method other than GET and HEAD needs a login by default, and `--allow-anonymous` writes a start-up warning (verify cannot see a server's log). Also cited, not cased: nothing is written to disk without `--directory`.
4. Reader: the generated Solution block lists all four `surl` commands, though only the first is the Solution; a sentence under it says so. Needs the lead if the build should print only the server the solution case uses.
5. Simplest solution (3): `--next` joins upload and fetch in one `curl` so the solution is one case and one command; two separate `curl` lines would read more plainly but the schema prints one command per solution. Author may prefer otherwise.
6. `<<ftp-protect-the-login-with-tls>>` points at a recipe not yet written.
