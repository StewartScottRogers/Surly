<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-download-every-file-in-a-folder-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Curl.Api has no -l: the long listing is parsed (first character, last word). The console's dot-file claim is a case here too; the log-free comparison uses Surl.Api's Files.ReadAsync.
