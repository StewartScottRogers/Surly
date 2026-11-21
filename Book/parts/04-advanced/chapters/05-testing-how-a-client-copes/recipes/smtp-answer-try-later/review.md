<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-answer-try-later
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Every Discussion claim is a case or cited (rfc5321 for 4xx temporary).
2. The two sends are one curl command with a URL glob; the retry is shown by the server's side (STATUS, Received line, log), not by curl's --retry (cased in smtp-survive-a-temporary-rcpt-failure).
3. The twice server's three cases depend on order: refused, refused with allowfails, then accepted.
