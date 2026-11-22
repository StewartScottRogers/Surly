<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-check-what-an-upload-stored-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 4 of 4 cases pass on windows (agent).

## Findings

1. Fitness (4): S3 over plain HTTP with SigV4 (no certificate); the stored bytes come back as text, compared with the uploaded file. A non-UTF-8 object would answer base64 instead of text; not run, so not claimed.
2. Truth (8): a replacing upload's wait can return the first upload's metadata event, so the program loops until the change is written (facts/s3.md); refusals leave nothing stored and read_file of the key is a tool error.
