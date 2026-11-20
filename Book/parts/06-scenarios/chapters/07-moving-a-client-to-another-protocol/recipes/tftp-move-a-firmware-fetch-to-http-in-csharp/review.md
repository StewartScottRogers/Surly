<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-move-a-firmware-fetch-to-http-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.

## Findings

1. Program finding: Curl.Api 0.2.0 cannot upload over TFTP. A PUT with a bytes or stream body to a tftp:// URL is sent as a read request (answer TftpNotFound, 68), not a write; on the console `curl -T` gets error 2, exit 69. The recipe does not claim a TFTP write from C#; it shows the HTTP 401 only and says why.
2. Binary image made with RandomNumberGenerator, so the comparison is byte for byte against the store.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: a Curl.Api PUT with a body to tftp:// is now a TFTP write (BL-5594); the write case shows it refused, CurlException TftpPerm 69, as the console's exit 69; finding 1 no longer holds. 5 of 5 pass.
