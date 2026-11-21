<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-restore-a-snapshot-before-each-test
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 5 of 5 cases pass on windows.
## Findings
1. Seed once (100 files, 3 mails), SnapshotAsync, RestoreAsync before each test; reversed order passes, no restore leaks, ResetAsync wipes the seed.
2. The plan says seeding 'takes a while'; seeding 100 files took about 20 ms here, so no timing is claimed.
3. FTP is read-only here: Curl.Api cannot upload or DELE over FTP (a DELE method sends RETR). FTP login needs AllowPlaintextAuthentication; the strict case shows 530.
4. Snapshot ByteCount is matched as a number, not a value.
