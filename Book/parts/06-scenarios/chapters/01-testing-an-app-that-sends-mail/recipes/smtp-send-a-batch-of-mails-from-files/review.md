<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-send-a-batch-of-mails-from-files
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The batch is one curl with a `-T` glob rather than a shell loop (the writer guide prefers single commands); the brace list covers arbitrary names.
2. Cases are order-dependent (counts 3 then 4).
