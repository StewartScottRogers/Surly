<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-check-logins-against-ldap
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, Libcurl 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Distinct from ldap-check-logins-against-a-directory: the angle is managing people in the directory alone at run time (control endpoint add and remove), plus a local account shadowing the directory.
3. The web server keeps a local ops account because of Surl BL-1663 (directory-only accounts serve GET with no login); the no-login case is 401 and the prose points to the sibling that shows the open server.
4. Run-time cases chain two curls with &&; given timeout 60 for slow curl start-up on Windows.
