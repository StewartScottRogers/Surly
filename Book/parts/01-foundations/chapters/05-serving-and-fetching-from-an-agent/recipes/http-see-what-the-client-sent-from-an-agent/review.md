<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-see-what-the-client-sent-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl-mcp 1.0.0 (Surl commit 3c5b39474), curl-mcp 0.1.0.0 (Curl commit b140f6d88c): 5 of 5 cases pass on windows.
## Findings
1. Fixed: the "max_exchanges defaults to 20" claim and the "unless userAgent names one" clause had no case, so I removed both.
2. Fixed: the second client uses `-A surly-reader` rather than curl's default User-Agent, so the exact-bytes claim does not break on every Curl version bump.
3. The recipe listens on {{port}} (shown as 8080), not port 0, so that the Host line can be matched exactly. The Discussion says why.
4. Needs the author (build tooling): build.py's mcp listing substitutes {{port}} in the arguments (shown as 8080) but not in the "answers" lines, which print a literal `{{port}}`. The solution listing and the claims table show the mismatch. The fix belongs in build.py (mcp_listing/mcp_result), not here.
5. Subset matching cannot prove that a list is empty or that it excludes an item. The scheme/listen_url filter claim is therefore positive only: it keeps exchanges 1 and 2. A refusal by filter (an empty answer) is not claimed.
6. Re-verified after the verify.py byte-exact fixtures fix: no claim here depends on fixture bytes (fixtures: {}; content goes in through start_server's files), so nothing changed.
