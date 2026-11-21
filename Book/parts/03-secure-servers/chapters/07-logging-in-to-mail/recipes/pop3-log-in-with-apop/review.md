<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-log-in-with-apop
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 cases pass on windows.
## Findings
1. The leak of password characters to a chosen timestamp is cited from --aihelp, not shown by a case.
2. curl rejects --login-options AUTH=APOP (exit 3); it uses APOP only when the server offers nothing it prefers, so the recipe uses a server with --auth apop alone.
3. The digest script needs a POSIX shell and openssl; checked on Windows (Git Bash) only.
