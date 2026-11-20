<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-poll-until-the-mail-arrives
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. Two POSIX sh scripts: slow-app.sh (backgrounded delayed send, output to /dev/null) stands in for the app; wait-for-mail.sh is the reusable wait (counted tries, no date +%s). Not yet run on Linux or macOS.
2. Cases one-search-is-too-early and arrives-later depend on the 3 s delay and each other (bob's mail); the wait allows 10 tries.
3. The --retry remark is cited to curl-manual, not cased.
