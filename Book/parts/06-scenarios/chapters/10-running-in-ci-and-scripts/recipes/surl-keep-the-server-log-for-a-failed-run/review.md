<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-keep-the-server-log-for-a-failed-run
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. surl now withholds FTP(S) PASS and HTTP(S) Authorization (HTTP/1.x and HTTP/2) from -v, the trace and --log-file as `****`; the step copies the trace as written. LDAP simple binds are still traced in clear (a case shows it), so the old awk filter is kept as drop-received.sh for such servers.
2. Program finding: with --trace-ascii given, the per-exchange log lines go to the trace file and --log-file gets only startup lines (decided by Surl, ADR-0033 s.6); so --log-file is not used here and the trace is presented as log and trace in one.
3. Uses POSIX sh scripts (tests.sh, ci-step.sh, drop-received.sh) shown with include::; the Solution says so.
4. Protocols are https, ftps and ldap: passwords over plain http/ftp are refused unchecked by default, and TLS keeps the recipe free of loosening options; the LDAP bind is refused unchecked and still traced.
5. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: surl withholds HTTPS and FTPS credentials from the trace; the awk filter is no longer needed for them and is kept only for LDAP, which still logs the bind password.
6. Re-pinned to Surl a423807a5d / Curl 0680b97577: surl now withholds the LDAP simple bind password (cn=app..****); the still-traced case became a withheld case, and drop-received.sh is kept for an artifact with no client bytes at all.
7. Bounded wait before reading the server's record (CI race sweep).
8. CI (macOS, run 45018591547): the line before `Login accepted: Basic app` was a 9-byte frame (`0000: .........`), not the withheld header block, which came earlier; the case now checks that a withheld block precedes each Basic and Bearer login, with the absent-secret case beside it.
