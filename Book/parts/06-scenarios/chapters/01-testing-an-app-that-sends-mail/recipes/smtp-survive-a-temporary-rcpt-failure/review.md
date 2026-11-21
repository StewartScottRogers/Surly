<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-survive-a-temporary-rcpt-failure
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited (<<rfc5321>> for 4xx temporary and 5xx permanent, <<curl-manual>> for what --retry counts as transient).
2. The sender is send-retrying.sh, a POSIX script (Git Bash on Windows); curl's own --retry does not retry an SMTP 451, and --retry-all-errors retries a 550 too, both cased.
3. Four servers, one per failure path, because the 451 rule has times=1 and is spent by the first send.
