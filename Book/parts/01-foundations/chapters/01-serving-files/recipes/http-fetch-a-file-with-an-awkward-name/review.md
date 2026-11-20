<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fetch-a-file-with-an-awkward-name
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 9 of 9 cases pass on windows.
## Findings
1. No program finding. A raw space is curl exit 3, a raw # is silently the fragment (request for /a), as curl does.
2. Raw UTF-8 é typed in a command line is not shown: how the bytes reach curl depends on the shell and code page (on Windows it arrived as Latin-1), so the recipe shows the Latin-1 %E9 404 instead.
