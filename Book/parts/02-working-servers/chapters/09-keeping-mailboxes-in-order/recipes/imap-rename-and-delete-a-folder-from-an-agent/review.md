<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-rename-and-delete-a-folder-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 12 of 12 cases pass on windows.
## Findings
1. APPEND through the curl tool (-T from fixtures) fills the folders; twelve cases against thirteen (delete-with-messages merged with its follow-up).
2. Refusal text read from curl -v.
