<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-log-in-with-apop-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 cases pass on windows.
## Findings
1. Same citation for the leak as the console recipe.
2. Bounded wait before reading the server's record (CI race sweep).
