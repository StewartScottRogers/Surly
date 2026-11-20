<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldaps-search-over-tls-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. One program, one in-process server with an ldaps and an ldap listener. Certificate trust is skipped with `VerifyPeer`/`VerifyHost` false, the same as the console twin's `-k`.
2. Program finding: on Windows `CertificateAuthorityBlob` (the way the HTTPS recipes trust a certificate) does not make the ldaps check pass: code 60 PeerFailedVerification, as `--cacert` does not for curl. Cased as `ca-blob-ignored`, Windows only. Linux and macOS not run.
3. The two refusals (plain ldap, wrong password) share code 38; only the message separates them (`Confidentiality Required` / `Invalid Credentials`), which is the library's wording and is matched as a substring.
4. `BoundUrls` entries are `Uri`, not `string`; `.ToString()` is needed to build a search URL.
