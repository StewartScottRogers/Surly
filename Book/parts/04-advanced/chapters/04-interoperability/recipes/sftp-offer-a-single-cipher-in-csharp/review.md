<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-offer-a-single-cipher-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 11 of 11 cases pass on windows.
## Findings
1. Truth: Curl.Api cannot narrow its client offer, so the refusals of a client are shown as the absence of the name in the server's recorded key exchange message (the offer read from `server.Exchanges`), not as a failed connect; the console twin shows the message `no matching cipher found`.
2. Fixed: Surl.Api has no property for `--ssh-ciphers`; `AdditionalArguments` carries them, and a refused one is a `SurlServerStartException` (FailedInit).
