<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-split-options-across-config-files-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 13 of 13 cases pass on windows.
## Findings
1. Finding: a property such as `MaxFileSize` is read before the config files and loses to them, `AdditionalArguments` after and win (case `property-before-loses`); the Surl.Api documentation may want to say so.
2. Fixed: upload names are distinct per scenario, since the served folder persists between cases and an existing name answers 204.
