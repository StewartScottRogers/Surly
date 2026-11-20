<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-failed-logins-in-the-log
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 8 of 8 cases pass on windows.
## Findings
1. Program finding (minor): the log does not say why a judged login was refused; a wrong password and an unknown account give the same 'Login refused: Digest <name>' line. The recipe says so and shows how to tell them apart by the account list.
2. The verbose log holds the Basic Authorization header (base64 of the password); a case shows it, and the Discussion warns.
3. The solution case comes after the five attempts that fill the log; run.sh therefore greps a fresh log.
