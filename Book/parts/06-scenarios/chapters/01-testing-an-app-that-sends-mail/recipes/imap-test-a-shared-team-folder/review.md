<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-test-a-shared-team-folder
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 11 of 11 cases pass on windows.
## Findings
1. Nearest honest reading of "a message delivered to it": a shared folder has no address (SMTP to team@ is accepted and discarded, case no-address), so the message is put there by IMAP APPEND (solution) or a member's MOVE from INBOX (case file-from-inbox). Said in the prose.
2. curl APPENDs with (\Seen), so the flags shown are \Flagged \Seen (case append-marks-seen). The fixture is CRLF because curl refuses --crlf on APPEND (case no-crlf-on-append, exit 25).
3. NAMESPACE and MOVE are cited to rfc9051 (IMAP4rev2 includes both); no rfc2342/rfc6851 entry in the bibliography.
4. Self-review against REVIEW.md; cases are order-dependent (UIDs 1 to 3 of Shared/team).
