<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-failed-logins-in-the-log
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Program finding (minor): the log does not say why a judged login was refused; a wrong password and an unknown account give the same 'Login refused: Digest <name>' line. The recipe says so and shows how to tell them apart by the account list.
2. The verbose log holds the Basic Authorization header with its credentials withheld (`Authorization: Basic ****`); cases show it and that no password is left.
3. The solution case comes after the five attempts that fill the log; run.sh therefore greps a fresh log.
4. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: surl withholds the Basic credentials from the -v log; the warning to keep the log private is replaced by a case showing the redaction.
