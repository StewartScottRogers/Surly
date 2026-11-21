<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-logins-from-basic-to-digest
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 of 13 cases pass on windows.
## Findings
1. Two servers: the move needs --allow-plaintext-auth (Basic over plain HTTP is otherwise 403 unchecked); the cutover is surl's default. The log names each Basic login's account; after the cutover a Basic request is logged only as a masked Authorization header and a 403 line, with no account.
2. Log counts depend on case order (cases share the server); the cutover count is the two Basic requests its cases send.
