<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-list-a-bucket

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 8 of 8 cases pass on windows.

## Findings

1. Decision: the certificate holds a wildcard `*.s3.us-east-1.amazonaws.com`, so one certificate serves any bucket name; Schannel accepts it.
2. curl drops a query string, so `?prefix=` and `?max-keys=` are not claimed; the prefix is the URL path.
3. Left out: ListBuckets (`s3://` with no host is refused by curl, exit 3), and the XML body, which curl turns into names.
