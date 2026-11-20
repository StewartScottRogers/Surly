<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-pass-any-option-through-in-process
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. --max-request-head (no property) shows AdditionalArguments working: 431 with it, 200 without.
2. AdditionalArguments are read after the properties: --no-allow-uploads overrides AllowUploads (201 vs 405).
3. Host options (--log-file) throw ArgumentException at StartAsync; surl refusals throw SurlServerStartException FailedInit with surl's line. The full host-option list is from the Surl.Api XML documentation, not a case.
4. Digest login is there only because uploads need one; said so.
