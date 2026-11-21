<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-a-dropped-download-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 9 of 9 cases pass on windows.
## Findings
1. Nearest honest form, as the console twin: the program writes the partial file.
2. Curl.Api has no resume option (no -C); the recipe spells it as PreQuote REST n with IgnoreContentLength, and shows PartialFile without the latter.
