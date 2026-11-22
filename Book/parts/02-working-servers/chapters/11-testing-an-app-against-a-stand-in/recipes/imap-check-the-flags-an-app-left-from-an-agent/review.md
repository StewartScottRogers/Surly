<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-check-the-flags-an-app-left-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 of 9, with surl-mcp 1.0.0, curl-mcp cases pass on windows.
## Findings
1. Each seed_mail is waited for by its store_changed event (10 s bound).
2. list_messages flags match Surl.Api's order (\Seen \Deleted); read_message sets no flag.
