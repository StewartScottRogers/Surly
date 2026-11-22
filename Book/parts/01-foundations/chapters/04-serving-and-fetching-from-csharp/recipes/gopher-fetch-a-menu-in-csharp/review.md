<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gopher-fetch-a-menu-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Menu lines end in CR LF; the program splits on CR LF.
2. With ListDirectories off the root is the error menu, not an error; the file is still served.
3. Bounded wait before reading the server's record (CI race sweep).
