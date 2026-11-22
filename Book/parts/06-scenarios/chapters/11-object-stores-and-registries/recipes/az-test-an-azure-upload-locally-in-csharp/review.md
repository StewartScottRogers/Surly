<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-test-an-azure-upload-locally-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. One program, twelve cases by argument, one per console claim; refusals are responses, not exceptions, over plain HTTP.
2. `SurlServerOptions` has no property for `--az-plaintext`: it goes in `AdditionalArguments`; `AllowPlaintextAuthentication` is the property for `--allow-plaintext-auth`.
3. Not claimed: Curl.Api's `az://` put, which is TLS only.
