<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tune-a-retry-policy-by-experiment
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, Libcurl 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Reading taken: the plan's 'growing server delays' is a growing outage (the first N requests answered 503, N = 1 to 3); the prose says a time-based outage would need its own sweep. No timing is claimed.
3. The sweep is about 35 s; timeout 300.
