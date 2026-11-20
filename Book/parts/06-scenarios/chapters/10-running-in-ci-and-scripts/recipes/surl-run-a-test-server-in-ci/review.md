<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-test-server-in-ci
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 windows cases pass (8 cases; clean-stop-unix is [linux, macos], left to CI).
## Findings
1. Same platform split as surl-start-and-stop-in-a-shell-script: the step counts surl exit 0 or 143 as a clean stop; 143 is Git Bash on Windows ending surl outright, not a surl choice.
2. Overlaps surl-hand-bound-ports-to-the-app (HTTP+SMTP on port 0); this one is about the step's exit code on every path: start refused (45), tests failed (67), surl ended early (137 from SIGKILL), clean stop.
3. No CI workflow YAML is shown: nothing could run it; the prose says the step runs the script.
