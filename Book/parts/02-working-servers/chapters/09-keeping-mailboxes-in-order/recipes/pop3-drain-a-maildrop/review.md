<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-drain-a-maildrop

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows (console).

## Findings

1. The drain is one curl: -o "msg#1.eml" on pop3://.../[1-3], then --next -X DELE on the same range; one POP3 session, so the DELEs commit at QUIT when curl exits.
2. A write failure (folder missing) is curl exit 2 before any DELE; the maildrop keeps its messages. The seed waits with LIST 3 and --retry, no sleep.
