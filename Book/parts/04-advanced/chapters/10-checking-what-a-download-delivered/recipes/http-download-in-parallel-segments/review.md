<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-in-parallel-segments
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.
## Findings
1. Close to http-download-a-file-in-parallel-ranges (part 6); this one takes the segment count as an argument and reads the ranges back from surl's -v log. The doubled-part claim is left to that recipe and pointed at.
2. Connections over loopback are claimed as 1 to 4; with the 1 s hold, exactly 4.
3. CI run 44906624868 (ubuntu-latest = Ubuntu 26.04, uutils coreutils): seen-by-server.sh printed no Range lines because uutils sort refuses `-t=` ("separator must be exactly one character long"); now `sort -t '='`. Not a log-flush race: the 206 event lines, written after the headers, were all there. Checked under WSL Ubuntu 26.04; 5 of 5 pass on windows.
