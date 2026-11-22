<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: xmpp-require-tls-before-login-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl-mcp 1.0.0 (Surl commit 5da5b3eaf) and curl-mcp (Curl commit c84035f797): 14 of 14 cases pass on windows.

## Findings

1. Carries all the console claims; raw clients are curl telnet:// -T with fixtures in the host folder. Adds two claims from get_exchanges, the decrypted record in order (STARTTLS offered, proceed, mechanisms, login), read after wait_for_event exchange_ended.
