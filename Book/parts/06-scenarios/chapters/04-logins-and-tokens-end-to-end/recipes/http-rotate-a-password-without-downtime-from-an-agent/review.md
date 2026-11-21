<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-password-without-downtime-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.

## Findings

1. add_account for an existing name is a tool error here (and Surl.Api throws), unlike the control endpoint, which replaces the password; the console recipe shows that difference.
2. remove_account of the last account answers {user, warning} with a second text block; a client taking `.Single()` text block throws. The program reads StructuredContent.
