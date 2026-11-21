<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-test-a-password-reset-mail
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The app stand-in reset-app.sh writes the page into the served folder and mails the link; check-reset-link.sh is the test. Both POSIX (od, tr, sed, printf); /dev/urandom is not POSIX but is on every target, Git Bash included.
2. Finding recorded in facts/mail.md: mailbox accounts log in on smtp, imap and pop3 only, so the HTTP listener on the same surl needs no login (200), which a reset link needs. The facts sheet had said any account makes HTTP 401; that holds for --user only.
3. The token is random, so the solution and wrong-token cases match patterns.
