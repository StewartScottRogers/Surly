<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-seed-a-file-tree-for-a-test
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. No program finding. Files.PutAsync after start makes folders; HTTP serves the files (folder 404), FTP lists them only with ListDirectories and needs AllowAnonymous; ListAsync needs neither.
2. A relative path is ArgumentException NotRooted (message includes Parameter 'requestPath').
