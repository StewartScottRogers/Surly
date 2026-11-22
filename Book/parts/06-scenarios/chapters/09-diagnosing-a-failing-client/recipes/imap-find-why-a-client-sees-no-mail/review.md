<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-find-why-a-client-sees-no-mail
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. Three causes, one program: a mistyped envelope recipient caught by `--mail-catch-all`, the same discarded with no catch-all (no store event at all; only the RCPT in get_exchanges), and mail already \Seen from another reader (store event `written` after `delivered`).
2. Waits on `wait_for_event` exchange_ended for smtp (10 s) before any read.
