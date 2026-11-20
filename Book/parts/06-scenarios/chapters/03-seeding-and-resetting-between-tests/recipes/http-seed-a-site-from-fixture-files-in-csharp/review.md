<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-seed-a-site-from-fixture-files-in-csharp

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.

## Findings

1. Program finding: Curl.Api cannot upload over FTP (PUT with a body to ftp:// sends RETR, status 0); writes go over HTTP and FTP reads, unlike the console and agent twins.
2. Fixtures are read from disk into Files, so the disk stays untouched by construction.
