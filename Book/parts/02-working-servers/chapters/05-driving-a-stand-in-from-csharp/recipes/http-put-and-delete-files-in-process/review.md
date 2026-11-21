<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-put-and-delete-files-in-process
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 cases pass on windows.
## Findings
1. Program finding: Files.DeleteAsync of the only file in a folder leaves the folder (listed empty, no Deleted event for it); the recipe states it as the behaviour.
2. Put and delete from the test need no login and no AllowUploads; a client DELETE with no login is 401.
