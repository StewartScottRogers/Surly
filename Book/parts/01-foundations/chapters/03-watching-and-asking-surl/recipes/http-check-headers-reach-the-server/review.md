<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-check-headers-reach-the-server
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. Each claim case sends hello.txt and trace.txt in one curl command (-o applies to the first URL only), so every case reads its own request with the pinned curl and cases do not lean on each other's order, except arrive-after-curls-own, which reads the solution's request.
2. Trace file sits in the served folder only so the cases can fetch it; the Discussion says to keep it outside in real use.
3. 'A header that arrives twice is the usual sign that a wrapper and its caller both added it' is advice, not a claim about either program.
