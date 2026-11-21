<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-follow-links-inside-the-served-folder
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.
## Findings
1. Links are made in `setup:` (a fixture cannot be a link) with `MSYS=winsymlinks:nativestrict`; on Windows that needs Developer Mode or an administrator, stated in the Discussion.
2. Absolute links are not claimed: Git Bash writes MSYS-style targets on Windows.
3. Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 404 now carries the body `404 Not Found` (BL-1444); outside-refused and chain-refused show it, and the Discussion no longer says the body is empty.
