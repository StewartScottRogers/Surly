<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gopher-walk-menus-into-subfolders
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. No program finding. The item type in a gopher URL is neither sent nor checked (1/hello.txt answers the file), as the facts sheet said.
2. The walk is a short sh script (needs a POSIX shell); it strips CR before awk.
