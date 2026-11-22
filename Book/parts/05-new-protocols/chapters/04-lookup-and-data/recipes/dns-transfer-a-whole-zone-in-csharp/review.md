<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-transfer-a-whole-zone-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 7 of 7 cases pass on windows.

## Findings
1. Curl.Api now has CurlDnsOptions (Transport, Json) on CurlRequest.Dns; used for TCP and JSON.
2. --allow-zone-transfer has no SurlServerOptions property: AdditionalArguments.
3. The console IXFR case is a CurlException 3 here.
