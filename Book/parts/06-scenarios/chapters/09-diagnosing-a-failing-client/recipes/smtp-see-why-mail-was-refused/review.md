<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-see-why-mail-was-refused
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 10 of 10 cases pass on windows.
## Findings
1. why-refused.sh (POSIX sh) waits for the exchange's 'ended' line in the -v log, at most 10 s, then prints each 4xx/5xx reply with the command and surl's reason line before it.
2. curl reports a 452 after DATA as exit 8 Weird server reply, naming neither command nor code: the server's log is the only place the reason appears.
3. The plan gives this recipe and its agent twin the same title; left to the lead.
