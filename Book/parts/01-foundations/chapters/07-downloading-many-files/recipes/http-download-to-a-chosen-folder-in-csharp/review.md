<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-to-a-chosen-folder-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. The program keeps the URL's whole path under the chosen folder (unlike curl -O); stated as the program's choice, as are exit codes 22 and 23.
2. Cases share one working folder, so the no-folder case writes under 'elsewhere' to stay independent of the solution's 'downloads'.
