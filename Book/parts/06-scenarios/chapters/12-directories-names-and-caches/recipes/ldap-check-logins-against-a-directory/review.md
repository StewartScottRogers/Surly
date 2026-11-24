<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-check-logins-against-a-directory
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Finding for Surl: with --user-ldap and no --user the server serves GET without a login (case directory-alone-asks-no-login); the recipe adds a local account, ops, and says why.
3. surl, not curl, is the LDAP client here; probed on Windows only, Linux and macOS for CI.
4. The directory's log shows an IOException ending each exchange: surl's client drops the connection without unbind (cosmetic).
