<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-a-small-website
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. Fixtures named .png, .jpg, .ico, .woff2 hold text; the Discussion says surl types by name only, which the cases show.
2. No index page: / is 404; pointed to http-list-a-directory.
