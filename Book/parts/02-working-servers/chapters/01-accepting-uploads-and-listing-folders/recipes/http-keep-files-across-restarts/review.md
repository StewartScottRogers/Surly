<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-keep-files-across-restarts
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 8 of 8 cases pass on windows.
## Findings
1. Needs the author (rubric 4/5): the restart itself is not a case. verify starts every server before the cases and stops them after; a second surl on the same folder is refused 124 while the first runs, and surl has no way to stop itself (the control endpoint is not served in this build), so a case cannot stop one and start another. The claim is narrowed to what is cased: the upload is a plain file on disk byte for byte; surl keeps no copy (a file changed on disk is served as it now is); a file present before start (`kept.txt`, the stand-in for an earlier upload) is served; in-memory surl starts empty. The Discussion says the restart follows from these and is not itself checked. A schema step that stops a server (e.g. `stop: main` in a case) would make it a case.
2. Rejected design: a second process reaching `site` through `--share` works while the first holds the lock, but relies on what looks like a lock gap (program finding), so it is not used.
3. Fixed: "nothing a previous run kept" overclaimed for a fresh server; the claim now says only that the store starts empty.
4. `.surl` keeping mail and retained messages, and the lock surviving exit, are cited to <<surl-readme>>.
