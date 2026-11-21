<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-share-one-account-file-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.

## Findings

1. SurlServerOptions.UserFile adds to Accounts; a bad file is SurlServerStartException naming the line.
