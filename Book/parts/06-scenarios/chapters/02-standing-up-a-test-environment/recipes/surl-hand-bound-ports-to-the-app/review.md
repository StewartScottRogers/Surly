<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-hand-bound-ports-to-the-app
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 3 of 3 cases pass on windows.
## Findings
1. No servers: block: every listener asks for port 0, so the cases run `sh run-tests.sh`, which starts surl, waits for its Listening on lines, exports the URLs and stops surl on exit. The scripts are fixtures and are shown in the prose with include:: (the build writes fixtures beside recipe.adoc).
2. Cases run `sh` scripts; on Windows verify runs them through cmd.exe and finds Git's sh on PATH. A reader on Windows needs Git Bash or WSL; said implicitly by `sh`, not as a claim.
3. Server-recorded check is surl -v's log line `Message stored: N bytes for 1 recipients`, kept in a temporary file.
4. Failure path: two runs sharing one --directory; the loser exits 124 and the script passes the code on. Order of the two runs is not deterministic, so the script prints the exit codes sorted.
5. Ran verify three times in a row; stable.
