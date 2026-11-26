<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-fixed-bearer-token-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): surl 1.0.0 (Surl master 1e5204d), Curl port 8.22.0 (Curl f2331ebe), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same pins: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. surl-mcp tool errors name the Bearer-token account properly (already exists / has no / does not exist).
3. remove_account of the last account answers a warning; the agent should treat it as a sign the server is open.
