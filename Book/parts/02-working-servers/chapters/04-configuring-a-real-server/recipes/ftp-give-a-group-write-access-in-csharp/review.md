<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-give-a-group-write-access-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The test makes the shared folder fresh with Directory.CreateTempSubdirectory and checks it on disk: Surl.Api server.Files does not see a share closed by ShareAccess (No folder at /team), reported to Surl.
3. SurlServerStartException.ExitCode prints as FailedInit (2) by itself.
