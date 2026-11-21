<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-accept-uploads-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 cases pass on windows.
## Findings
1. Refusals are statuses (curl exits 0): 409, 401, 401, 403 for Basic, 405 with uploads off; no account is 401 even with uploads on.
2. get_events store_changed records the upload as change written, name /notes.txt.
