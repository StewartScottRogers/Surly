<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-refuse-to-reset-a-real-directory
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Anonymous upload needs AllowUploads and AllowAnonymous (an account makes GETs 401 as well); the loosening is named in the Discussion.
3. The exact InvalidOperationException messages for reset and snapshot are claimed; the folder listing shows nothing was deleted.
