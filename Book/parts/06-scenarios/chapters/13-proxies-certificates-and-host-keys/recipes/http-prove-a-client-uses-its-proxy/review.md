<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-prove-a-client-uses-its-proxy
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 6 of 6 cases pass on windows.
## Findings
1. The proof is the Via: 1.1 surl header on every origin-form request in --trace-ascii output, counted by a short awk fixture (included in the Discussion); the bypass case shows the check failing with exit 1.
2. The client under test is curl -x in seed:; a reader substitutes their program.
