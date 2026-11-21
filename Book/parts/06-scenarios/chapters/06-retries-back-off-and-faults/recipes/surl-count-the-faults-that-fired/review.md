<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-count-the-faults-that-fired
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. One server, http and ftp listeners, four rules; FaultApplied per RuleId vs Faults.List().
2. Finding: SurlFaultRule.Item gives 'error;scheme=http;path=%2Freport.txt;reply=503' (path percent-encoded, times dropped); expected the rule as given. Claimed as observed.
3. A wrong password does not spend refuse-login (see ftp-retry-after-a-refused-login-in-csharp); here the password is right.
Re-pinned to Surl a423807a5d / Curl 0680b97577: SurlFaultRule.Item keeps / in path= (Surl BL-1539); finding 2's percent-encoding is fixed, times still left out.
