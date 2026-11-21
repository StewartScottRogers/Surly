<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-skip-unchanged-files-with-etags-from-an-agent

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 5 of 5 cases pass.

## Findings

1. Same retitling. Uses the curl tool with -R -z; waits 1.1 s before put_file because Last-Modified counts whole seconds.
2. No same-second or older-date case: put_file cannot set a date.
