<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-keep-a-pinned-host-key-across-restarts-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 7 of 7 cases pass on windows.
## Findings
1. Restarts are three SurlServer starts on port 0; LogLevel.Verbose gives the 'Serving SSH host key' line.
