<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-choose-a-block-size
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. Not run on Linux or macOS; nothing in the claims is platform-specific and no platforms: is set. UDP to a closed port (Windows exit 7 at once, other platforms retransmit) is not claimed.
2. The server log is read through a second surl on the logs folder: reading surl.log from the folder it is written in logs each read and grew the file, a read took 10 to 15 seconds and grew each time (noted in facts/websocket.md).
3. The 65464 ceiling is stated as the size the server agrees, with no claim about which side clamps; Curl.Api refuses a larger BlockSize at once (see the C# twin), curl sends 65464.
4. RFC 2347 and 2348 (options, blksize) are not in the bibliography, so the prose names the extension without a citation.
