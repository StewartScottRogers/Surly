<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-uploads-to-s3-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 of 13 cases pass on windows.
## Findings
1. Carries the console claims plus a store read (server.Files.ReadAsync); logins from LoginJudgedEvent with Scheme https/s3; AuthenticationMethods (null by default) assigned a new list for the cutover.
