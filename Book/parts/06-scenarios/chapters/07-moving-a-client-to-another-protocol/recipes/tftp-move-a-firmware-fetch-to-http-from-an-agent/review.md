<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-move-a-firmware-fetch-to-http-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.

## Findings

1. `transfer` reaches tftp:// (statusCode 0); binary bodies come back base64, compared as strings. A missing TFTP file is a tool error, kind CurlExit, text with two lines (`TFTP: File Not Found` then `curl exit code 68 (TftpNotFound)`), so the case matches by regex.
2. Writes go through the `curl` tool (`-T up.txt`): exit 69 over TFTP, 22 over HTTP with -f.
