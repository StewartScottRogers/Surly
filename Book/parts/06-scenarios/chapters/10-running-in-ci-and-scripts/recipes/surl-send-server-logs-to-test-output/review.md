<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-send-server-logs-to-test-output
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 2 of 2 cases pass on windows.
## Findings
1. No test framework package: a stand-in runner keeps per-test output and prints it on failure, as xUnit/NUnit do; the adapter takes an Action<string> such as ITestOutputHelper.WriteLine.
2. The verbose log holds request headers (secrets) in this build; the Discussion warns and points to the keep-the-log recipe. Surl BL-1417 (redaction) would change that.
