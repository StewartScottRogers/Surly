<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-load-mailboxes-from-a-csv-file
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 of 11 cases pass on windows.
## Findings
1. Finding for Surl: a protocols cell naming a scheme with no login (gopher) is refused at start without the file and line: `surl: (2) scheme gopher has no login`, while an unknown scheme gets `Mailbox file f.csv, line 2: unknown scheme nosuch`. Not claimed.
2. The forty mailboxes are written by make-mailboxes.sh in setup:; the start-up log lines are written before any listener binds, so reading them after readiness needs no further wait.
3. Line numbers count physical lines: blank lines and a quoted field spanning two lines both shift them (probed; the blank line is claimed).
