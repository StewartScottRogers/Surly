<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-serve-a-plain-http-endpoint

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.

## Findings

1. Decision: the Solution reaches the plain listener with a path-style `http://` URL and `--aws-sigv4 aws:amz:us-east-1:s3`; curl's own `s3://` is TLS and ends at exit 35 against it (a case). The warning is read from `--log-file`.
2. Without `-f` an S3 error over http is curl exit 0 with the XML body, so the cases match `<Code>`. With `-f` it is exit 22 and curl names only the status.
