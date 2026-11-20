<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-seed-a-mailbox-before-a-test-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0 (local feed): 4 of 4 cases pass on windows.
## Findings
1. Delete through Curl.Api: CurlRequest with method DELE on pop3://.../1. The deletion lands only when the CurlClient is disposed (QUIT on close); kept as case delete-on-quit, a real trap for tests.
2. Failure path: Mail.DeliverAsync to an address with no mailbox throws SurlSeedRefusedException NoSuchAccount.
