<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-stop-servers-cleanly-in-teardown
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. The test 'throws' with the honest FTP refusal (LoginDenied, no account) rather than a contrived exception.
2. clients-last shows StopAsync waiting while a kept-alive connection is open (over 3 s, Surl ADR-0099: 5 s grace + 2 s linger); teardown disposes clients first.
3. The stop-failure branch (a StopAsync that throws) has no case; described as what the code does.
