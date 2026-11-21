<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-mail-for-unknown-addresses
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Overlaps smtp-catch-every-address-in-one-mailbox (part 06, POP3 counts); this one is the server's routing rules: per-domain catch-all, aliases and real mailboxes win, letter case ignored, no envelope record in the caught copy, one catch-all per domain.
2. Each read after a send retries the expected UID (exit 78) or FETCH FLAGS with --retry, bounded at ten tries.
3. The caught copy carries no Delivered-To or X-Original-To, so a Bcc'd stray cannot be found by address; stated as a limit, possibly a Surl feature request.
