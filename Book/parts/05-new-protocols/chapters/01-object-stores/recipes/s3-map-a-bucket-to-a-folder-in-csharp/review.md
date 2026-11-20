<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-map-a-bucket-to-a-folder-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.

## Findings

1. Program finding: Curl.Api cannot put to an `s3://` URL (`HttpMethod.Put` with a body is `CurlException` 4 `NotBuiltIn`, `s3:// does not support -X PUT; it sends only GET, or PUT with -T`; Curl.Api has no `-T` spelling). The put claim is replaced by two: a file the test puts into the folder with `server.Files.PutAsync` is an object, and the refused PUT is shown.
2. `SurlServerFiles.PutAsync` and `ReadAsync` need a leading slash on the path.
