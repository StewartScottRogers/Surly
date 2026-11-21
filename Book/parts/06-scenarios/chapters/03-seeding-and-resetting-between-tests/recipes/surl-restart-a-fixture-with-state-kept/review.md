<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-restart-a-fixture-with-state-kept
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. Mail is kept under <dir>/.surl/mail with --directory; restart: kills the process outright, so the claims hold after a crash-like stop. UIDVALIDITY was the same across restarts in a probe but is not claimed (a value that cannot be compared across two cases).
