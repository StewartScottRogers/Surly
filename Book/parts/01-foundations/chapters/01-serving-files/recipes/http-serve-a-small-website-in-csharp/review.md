<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-a-small-website-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. The PNG fixture is the real 8-byte signature, to show non-UTF-8 bytes round-trip.
2. The root case passes '' as the path, and the program's own check fails it with exit 1, as a test would.
3. Re-pinned to Surl a423807a5d / Curl 0680b97577: the root's 404 is now 14 bytes of text/plain (BL-1444).
