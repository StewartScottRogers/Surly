<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-an-app-sent-the-right-mail
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 7 of 7 cases pass on windows.
## Findings
1. Distinct from smtp-check-the-mail-an-app-sent (header fields) and smtp-test-a-password-reset-mail (follows the link): this one judges the recipient by --mail-delivered-to's Delivered-To line, the link by IMAP SEARCH BODY and grep -o, and waits for each mailbox with a bounded --retry.
2. Every read of the store waits first: seed lines retry FETCH 1 FLAGS (sets no flag), the solution retries its own fetch; the never-delivered case is bounded to --retry 2 (about 2 s, exit 78).
