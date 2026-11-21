<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-keep-a-mailbox-off-smtp-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 cases pass on windows.
## Findings
1. Delivery is awaited with wait_for_event (name viewer@example.com/INBOX/;UID=1) before the reads.
