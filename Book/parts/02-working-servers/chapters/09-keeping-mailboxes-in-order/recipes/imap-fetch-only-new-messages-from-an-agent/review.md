<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-fetch-only-new-messages-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 of 9, with surl-mcp 1.0.0, curl-mcp cases pass on windows.
## Findings
1. Each pass waits until get_events counts as many exchange_ended as exchange_started (wait_for_event, 10 s each) before get_exchanges after_exchange_id.
2. list_messages after_uid gives the expected new UIDs; uid_validity equals STATUS UIDVALIDITY.
