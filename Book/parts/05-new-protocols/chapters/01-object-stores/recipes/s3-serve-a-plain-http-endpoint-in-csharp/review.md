<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-serve-a-plain-http-endpoint-in-csharp

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.

## Findings

1. `SurlServerOptions` has no property for `--s3-plaintext`: `AdditionalArguments = { "--s3-plaintext" }`. The warning is read from `Log` (a `StringWriter`). `SurlServerStartException.ExitCode` is an enum, cast to `int` to print 58.
2. Over `http://` Curl.Api returns a refusal as a response (403 with XML), not a `CurlException`; an `s3://` URL against the plain listener is `SslConnectError` (35).
