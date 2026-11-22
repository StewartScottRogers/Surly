<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-upload-a-file-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 12 of 12 cases pass on windows.
## Findings
1. Fixed: relies on Curl 7d167fa10a sending a `PUT` with a body to `tftp://` as a TFTP write (BL-5594); before it was sent as a read request (exit 68).
2. Truth: the error code 2 is read from the recorded exchange's datagrams (opcode 5), not guessed from the exception code.
3. Bounded wait before reading the server's record (CI race sweep).
