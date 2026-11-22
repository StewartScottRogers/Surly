<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-log-in-with-tls-srp-in-csharp
Verdict: accept with findings (nearest honest recipe)
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 7 of 7 cases pass on windows.
## Findings
1. As the console twin: Curl.Api still has CurlAuthenticationOptions.TlsAuthType/TlsAuthUserName/TlsAuthPassword but they have no effect (Curl BL-2152 removed the SRP client); SendAsync throws SslConnectError. No login is claimed.
2. Adds the live account API, SurlServerTlsSrpAccounts Add/SetPassword/Remove, and InvalidOperationException on a non-SRP server; none of it can be tied to a handshake with these builds.
3. SurlServerOptions has no TLS-SRP property: AdditionalArguments.
