<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-file-mail-into-a-folder-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 9 of 9 cases pass on windows.
## Findings
1. Seeds with seed_mail and waits for each store_changed event with a ten second bound; refusal text is read from curl -v.
2. Same nine claims as the console recipe.
