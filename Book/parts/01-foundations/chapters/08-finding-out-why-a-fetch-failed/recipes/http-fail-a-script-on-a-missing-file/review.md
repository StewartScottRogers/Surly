<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fail-a-script-on-a-missing-file
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. Planned task said --fail-with-body keeps the error page; surl's 404 (and every error reply, --fault included) has Content-Length 0, so the recipe shows --fail-with-body exiting 22 with 0 bytes and says there is nothing to keep. Nearest honest recipe.
2. The 503 case needs a second server with --fault (warns, tests only); it has no --directory because two surl processes cannot share one folder (exit 124).
3. '-s alone hides the error message' is cited to curl-manual, not a case.
