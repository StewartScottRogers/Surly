<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-and-read-it-over-imap
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 12 of 12 cases pass on windows.
## Findings
1. Fitness (1): the send and the read are one `curl` joined by `--next`, so the Solution block shows the whole task and run.sh/run.ps1 run it with the pinned `curl` only (an `&&` between two `curl` commands would leave the second unpinned and fail in Windows PowerShell 5.1).
2. Fitness (3): `--crlf` is not there for the test: without it a file with plain line feeds is stored with a stray blank line at the end (curl's end-of-data sequence), so it is the advice a reader needs. The Solution fetches `;SECTION=TEXT` so its output is exact; the whole message carries a dated `Received` line and is a `stdout-matches` claim.
3. Truth (4, 7): the default refusals (SMTP 530/exit 55, IMAP NO/exit 21, POP3 -ERR/exit 8, login exit 67, LOGINDISABLED) are cases against a second, strict `surl`. Cost: the Solution listing shows two `surl` lines; the prose says the second is for the claims only. The start-up warning is cited to <<surl-readme>>, not cased (verify cannot read a server's log).
4. Reader: accounts, STARTTLS and TLS-only listeners are deferred to Part 3 by pointer, not explained here.
