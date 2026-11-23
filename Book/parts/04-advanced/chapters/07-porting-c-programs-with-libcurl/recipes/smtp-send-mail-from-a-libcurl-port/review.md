<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-mail-from-a-libcurl-port
Verdict: accept (self-review)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), dotnet 11.0.100, Libcurl 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. The mailbox is read over POP3 with --retry 10 --retry-all-errors --retry-delay 1: bounded wait, no fixed sleep.
2. protocols lists pop3 beside smtp, since the test's check reads POP3.
3. The C listing is not compiled by verify.
