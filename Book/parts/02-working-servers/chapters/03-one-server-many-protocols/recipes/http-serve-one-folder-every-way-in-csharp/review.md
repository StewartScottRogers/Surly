<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-one-folder-every-way-in-csharp
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. A relative Uri of 'd:hello.txt' throws UriFormatException (read as a DOS drive) on Windows; the program joins strings instead, and says why.
2. DICT reply is 157 bytes in-process (no ready probe, greeting <5@surl>).
