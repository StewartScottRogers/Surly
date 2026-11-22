<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-load-mailboxes-from-a-csv-file-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 of 11, with dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0 cases pass on windows.
## Findings
1. Each run writes its CSV into Directory.CreateTempSubdirectory and deletes it; Lines[0] names the file by the full path MailboxFiles gave, which the program strips for stable output.
