<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-start-a-mailbox-with-folders-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. Curl.Api cannot APPEND or DELETE at 0.2.0, so the twin shows folders via LIST and the store, and ResetAsync keeping them, instead of filing a message in Sent.
