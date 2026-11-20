<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: telnet-hold-a-line-session

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. `-T file` is used in place of a `<` redirect so the run.ps1 listing is portable; the output is the same.
2. A file with no quit hangs curl: `-m 2` gives exit 28 with the echo already written.
3. The terminal-type and environment claims read surl's own log back with `grep` (the log is in the recipe folder), so they need `grep`, present with Git on Windows. Added rfc854 to references/bibliography.adoc.
