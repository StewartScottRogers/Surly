<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-choose-passive-or-active-mode
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 12 of 12 cases pass on windows.
## Findings
1. The plan says to see each mode in surl's log. A console case cannot read surl's stderr, so the server runs with -v and --log-file into the served directory and one case reads the log back over FTP; it shows passive and active data connections, each after its RETR.
2. The refusal for --allow-anonymous is a case on a second server (no --directory, since two surls cannot share one: exit 124).
3. RFC 2428 (EPSV, EPRT) is not in the bibliography; named in prose only, RFC 959 cited. Needs the author.
4. Program finding: curl -P 192.0.2.1 prints 'bind(port=0) on non-local address failed' on the client side and still sends EPRT; surl answers 501 and curl exits 30. Recorded as a claim, not a bug.
