<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-upload-and-rename
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 11 of 11 cases pass on windows.
## Findings
1. Fixed: the Solution is the upload-then-rename pattern (`-Q "-rename ..."` runs after the transfer); the absence of `notes.part` is proved by an exact listing, not a file check.
2. Fixed: both loosenings have their refusal as a case on a second server (port 8081): SFTP upload exit 9, SCP upload exit 25, rename exit 21 with the file unchanged, listing exit 78; downloads still work.
3. Fixed: removed "SCP has no rename", which no case proved.
4. Long listing checked by pattern, since dates vary. The generated Solution's expected output does not show the `--throwaway-hostkey` warning line surl prints on start (build, all such recipes).
