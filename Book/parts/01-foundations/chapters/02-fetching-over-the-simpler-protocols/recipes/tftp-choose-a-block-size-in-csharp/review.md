<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-choose-a-block-size-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. Datagram boundaries are SurlExchangeRecord.Chunks; the program reads opcodes 1, 3, 5 and 6 from them.
2. CurlTftpOptions.BlockSize throws ArgumentOutOfRangeException outside 8 to 65464; the console curl accepts 70000 and sends 65464. Noted as a difference, not a defect.
3. Not run on Linux or macOS.
Re-pinned to Surl a423807a5d / Curl 0680b97577: CurlTftpOptions.BlockSize now follows libcurl (Curl BL-5616): 70000 is clamped to 65464, 4 falls back to 512, only a negative value throws; finding 2 no longer holds.
