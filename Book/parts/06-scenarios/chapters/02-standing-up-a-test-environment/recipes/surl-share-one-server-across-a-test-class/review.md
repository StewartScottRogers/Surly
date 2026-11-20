<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-share-one-server-across-a-test-class
Verdict: accept with a narrowed premise
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 3 of 3 cases pass on windows.
## Findings
1. The plan's premise ('starting a server per test is slow') does not hold strongly here: in-process StartAsync took about 5 ms warm (about 0.4 s for the first in a process), ResetAsync 0 to 20 ms. The Problem is reworded (wire the app once, clean state per test); no timing is claimed.
2. Order independence shown by running the tests reversed; leakage shown without the reset.
