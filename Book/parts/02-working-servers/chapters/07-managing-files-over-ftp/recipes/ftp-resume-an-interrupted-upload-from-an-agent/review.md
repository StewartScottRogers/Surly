<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-an-interrupted-upload-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 10 cases pass on windows.
## Findings
1. Local and seeded files are letters, not random bytes, so they travel as text through `files` and `read_file`.
2. `reads-size-first` uses `curl -I`'s `Content-Length`.
