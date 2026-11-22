<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-rename-and-delete-a-folder
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 13 of 13 cases pass on windows.
## Findings
1. Task said to show the refusal for renaming INBOX, but surl accepts RENAME INBOX the way RFC 3501 asks: messages move to the new folder and an empty INBOX stays. The recipe says so and shows DELETE INBOX (NO [CANNOT]) as the refusal.
2. RENAME onto an existing name is NO [ALREADYEXISTS]; a missing source or target of DELETE is NO [NONEXISTENT]; DELETE of a folder with messages is silent and takes them. An implied parent (Work) is not a folder and cannot be deleted.
