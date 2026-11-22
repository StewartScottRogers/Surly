<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-sign-a-run-of-s3-requests-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Curl.Api DELETE on s3:// throws NotBuiltIn, as the console's exit 4; the delete uses CurlAuthenticationOptions.AwsSigV4 over https://.
Re-pinned to Surl a423807a5d / Curl 0680b97577: Curl.Api now sends DELETE on s3://; the NotBuiltIn case became a wrong-secret delete (HttpReturnedError 403) and a signed one (204, store empty). Finding 1 no longer holds.
