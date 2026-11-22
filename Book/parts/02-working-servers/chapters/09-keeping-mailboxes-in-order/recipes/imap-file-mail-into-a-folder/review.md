<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-file-mail-into-a-folder
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 9 of 9 cases pass on windows.
## Findings
1. Delivery is asynchronous, so the seed waits for message 3 with a bounded curl retry on FETCH 3 FLAGS; the cases run in order on one mailbox and each says what it leaves.
2. EXPUNGE prints `* n EXPUNGE` on stdout; the expected output shows it. COPY keeps flags; MOVE is advertised and answers OK MOVE completed.
3. COPY to a missing folder is NO [TRYCREATE]; the case runs while INBOX still has messages, since an empty INBOX answers BAD for the message number first.
