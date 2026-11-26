<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-catch-a-duplicate-from-a-lost-reply
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp, curl-mcp from the same builds: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The rule is added through the control endpoint (POST /v1/faults, text/plain) after verify's port check: on the command line, the after-bytes rule (accept stage) was spent by the readiness probe and nothing was cut.
3. The 320 is derived, not guessed: tries.sh sums the trace's Send data sizes, 319 at the end of the 354 in both tries.
4. tries.sh keeps $ out of sh -c (script file); the run lines use && and ||, which cmd.exe and sh both read.
