<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-map-a-bucket-to-a-folder-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.

## Findings

1. The put is a Curl.Api PUT to s3://pics/up.txt, checked in the folder with server.Files.ReadAsync and through the bucket gallery.
2. `SurlServerFiles.PutAsync` and `ReadAsync` need a leading slash on the path.

Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: Curl.Api now PUTs to s3:// (BL-5594); the refused-PUT case and the SurlServerFiles.PutAsync workaround became a real PUT read back from the folder.
