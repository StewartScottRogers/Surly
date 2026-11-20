<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-set-client-defaults-once
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. ConnectTimeout is set and named (--connect-timeout) but no case shows it firing: a connect that hangs needs a non-loopback address, which CI cannot rely on.
2. The timeout case claims only that the 2 s Timeout fires before the server's 5 s delay, not an exact time.
3. Program note: a client with no options sends no User-Agent (the curl command sends curl/8.22.0).
