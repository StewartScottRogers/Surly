<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-two-servers-in-one-program
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. The ports are not printed exactly (they vary); the program prints 'two ports' when they differ.
2. The BindFailed case uses the first server's BoundUrl, the realistic collision; message text varies by platform, so only ExitCode is checked.
