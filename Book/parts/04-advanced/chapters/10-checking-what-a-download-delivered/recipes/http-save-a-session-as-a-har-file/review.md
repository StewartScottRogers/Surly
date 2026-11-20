<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-save-a-session-as-a-har-file

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 8 of 8 cases pass on windows.

## Findings

1. Nearest honest recipe: neither surl nor curl writes HAR (no `--har`, no MCP tool). A sh script builds HAR 1.2 from curl `-v`, `-D`, `-o`, `-w`.
2. Well-formedness is checked with Python's json module (`python3` in the case); not loaded into a browser. Request bodies, cookies and header sizes are left out, stated.
3. Bibliography has no HAR 1.2 entry (W3C Web Performance draft); the recipe cites none for the format.
