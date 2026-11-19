<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-list-and-upload
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 14 of 14 cases pass on windows.
## Findings
1. Fixed: the refusals read surl's replies with `-v` (`550 No such directory`, `550 Not permitted`, `530 Login needs TLS first`) instead of exit codes alone.
2. Fixed: without `--allow-anonymous` the anonymous login is refused because its password would travel in clear (530 Login needs TLS first), not as "anonymous" as such; the claim says that.
3. Fixed: "overwrite" removed from why `--allow-anonymous` is not the default; no case replaces a file over FTP.
4. Exit 19 for a refused listing and 9 for a missing folder match upstream curl's mapping of a 550 on LIST and on CWD.
5. Long listing is checked by pattern, since dates vary. Generated Solution block also starts the two refusal servers (build finding). <<ftp-protect-the-login-with-tls>> is not yet written.
