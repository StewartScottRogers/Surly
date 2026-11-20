<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-call-with-a-proto-file-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. Plan task changed, nearest honest recipe: the library has no `.proto` option. Curl.Api 0.2.0 calls a `grpc://` URL by reflection and sends an empty request: `GET` gives `{}`; a `POST` body is not sent (status 0 and `{}`), and a `Content-Type` header makes it fail with `HTTP status 415`. Libcurl 0.2.0 answers `UnsupportedProtocol` for `grpc://` and `kafka://`. Program findings, reported to the lead; not claimed in the book.
2. So the 'file' is the field numbers in the program, over the framework's `HttpClient` (HTTP/2 with prior knowledge, `TE: trailers`), and reflection is called by hand with `list_services` (field 7). Curl.Api is used for the reflection call and for its two refusals (`CurlException` 22 `HttpReturnedError`, 2 `FailedInit`).
3. Fitness: the program is longer than the model (about 110 lines) because of the by-hand framing; each helper is one short function.
