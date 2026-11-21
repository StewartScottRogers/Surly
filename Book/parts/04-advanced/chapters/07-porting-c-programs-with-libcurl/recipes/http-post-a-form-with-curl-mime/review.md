<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-post-a-form-with-curl-mime
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Libcurl 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. surl serves no form: the POST is answered 405 after its body is read, and the test reads the parts from `--trace-ascii`. Nearest honest recipe to "the server takes the form"; stated in the Discussion.
2. Depends on surl reading the whole body before closing on the 405; it did on Windows. If Linux or macOS reset the connection early, curl_easy_perform could fail there: watch CI.
3. `curl_mime_init` moves from the easy handle to `library.MimeInit()`; stated.
