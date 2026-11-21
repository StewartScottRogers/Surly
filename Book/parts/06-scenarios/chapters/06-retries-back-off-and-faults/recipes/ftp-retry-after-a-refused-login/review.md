<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-retry-after-a-refused-login
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. Each server whose rule a case spends has its own folder; times=1 refuse-login is not spent by the readiness connection (login stage).
2. curl --retry does not retry 530 (exit 67); --retry-all-errors does. The 4xx-is-transient rule for FTP is cited from the curl manual, not shown (surl has no 4xx login refusal here).
3. A login refused by refuse-login leaves no log line naming the rule (only 'Login refused: ftp app'); counted as such.
4. Without --allow-plaintext-auth the clear password is refused with no rule: case plaintext-refused.
