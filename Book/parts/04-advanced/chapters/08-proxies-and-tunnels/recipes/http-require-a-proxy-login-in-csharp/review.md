<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-require-a-proxy-login-in-csharp

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.

## Findings

1. Carries the console claims; one extra: LoginJudged events show the proxy (Digest app) and origin (Digest web) logins of one fetch apart.
2. SurlServerOptions has no proxy or proxy-account properties: --proxy and -U go through AdditionalArguments; CurlAuthenticationOptions.ProxyUserPassword/ProxyAuthentication and CurlTlsOptions.Proxy do the client side.
3. Program trap fixed during writing: the response body must be read before its CurlClient is disposed (OperationCanceledException otherwise).
