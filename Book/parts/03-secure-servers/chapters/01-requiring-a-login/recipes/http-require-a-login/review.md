<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-require-a-login

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 10 of 10 cases pass on windows.

## Findings

1. Fixed: Part 1's `http-serve-a-directory` says a server with no account needs no login and one with an account needs one for every request. Both are cases here now (`no-account-no-login`, `no-login`, `head-needs-login`), with a second server, `open`, on its own folder: two surl processes cannot hold one `--directory`.
2. Fixed: "every HTTP request needs a login" is cited to the README; the cases prove GET and HEAD only.
3. Fixed: the Basic-over-http pointer is a case (`basic-refused`, 403) rather than a bare forward reference.
4. Not claimed: the README says that with no account "every login is refused". `curl --digest` never sends credentials to a server that does not challenge, so no clean case exists; left out.
5. The empty-password case's `run:` is quoted in YAML because `alice: ` would otherwise parse as a mapping.
