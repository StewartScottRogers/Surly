<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-in-parallel-segments
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.
## Findings
1. Close to http-download-a-file-in-parallel-ranges (part 6); this one takes the segment count as an argument and reads the ranges back from surl's -v log. The doubled-part claim is left to that recipe and pointed at.
2. Connections over loopback are claimed as 1 to 4; with the 1 s hold, exactly 4.
