<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-mirror-a-folder-with-a-script
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.
## Findings
1. One curl with a -T pair per file keeps the run fast (each curl process costs about a second on the Windows runner); --fail-early makes it stop at the first failure.
2. With uploads off the first MKD is refused, so the script exits 9, not 25.
