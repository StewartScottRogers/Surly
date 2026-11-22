<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-an-interrupted-upload-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 cases pass on windows.
## Findings
1. Curl.Api has no resume option: the program does the `SIZE` (HEAD, `Content-Length`) then a Put with `Append = true`.
2. Finding: Curl.Api's HEAD on an FTP URL sends `REST 0` after `SIZE` (console `curl -I` does not); the recipe says so in the claim.
3. Fewer cases than the console recipe: its `cmp` follow-ups are folded into `same as local`.
