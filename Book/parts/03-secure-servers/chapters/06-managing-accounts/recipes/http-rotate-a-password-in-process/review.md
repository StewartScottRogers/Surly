<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-password-in-process

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.

## Findings

1. The task's 'drop the old account and add the new one' is Remove then Add; Add for an existing name throws ArgumentException, shown as a case. The zero-refusal alternative is pointed to, not repeated.
