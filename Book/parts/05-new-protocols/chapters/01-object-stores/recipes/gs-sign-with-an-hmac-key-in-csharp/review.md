<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gs-sign-with-an-hmac-key-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.

## Findings

1. Program finding: Curl.Api cannot send a body to a `gs://` URL: `HttpMethod.Put` with a `Body` throws `CurlException` `NotBuiltIn`, `gs:// does not support -X PUT; it sends only GET, or PUT with -T`, although the same put to `s3://` works (BL-5594) and `curl -T` to `gs://` works on the console. Expected: as `s3://`.
2. So the C# recipe's puts go over `https://storage.googleapis.com/...` with a bearer token, and the HMAC-signed put of the console recipe has no C# case; two cases replace it (the refused put stores nothing; a put with an unknown token is refused with uploads on).
3. Over `https://` the token is sent only with `HttpAuthentication = HttpAuthenticationMethods.Bearer`; over `gs://` `XOAuth2Bearer` alone is enough.
