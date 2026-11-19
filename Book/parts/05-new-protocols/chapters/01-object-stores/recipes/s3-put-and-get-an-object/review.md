<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-put-and-get-an-object

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 13 of 13 cases pass on windows.

## Findings

1. Decision: `curl` reaches `surl` through `--connect-to ::127.0.0.1:8080`, not `AWS_ENDPOINT_URL`. The Curl port's `s3://bucket/key` takes the bucket as the URL's host and sends it to `bucket.s3.<region>.amazonaws.com:443`; the only other endpoint setting is the environment variable `AWS_ENDPOINT_URL` (or `AWS_ENDPOINT_URL_S3`), which a recipe's one-line command cannot set for both shells. `--connect-to` with empty host and port sends every connection to `surl`, so a stray region cannot send a request off the machine. The certificate therefore names `photos.s3.us-east-1.amazonaws.com` as well as `127.0.0.1`. The lead's first probe (`s3://127.0.0.1:PORT/site/hello.txt`) failed because the URL's host is the bucket, and because `site` was the served root, not a bucket in it.
2. Not in the Discussion, for want of a portable case: `AWS_ENDPOINT_URL=http://...` with `surl --s3-plaintext` (probed: works, exit 0), and that a reader's own `AWS_REGION`, `AWS_ENDPOINT_URL` or `AWS_ACCESS_KEY_ID` changes where and how `curl` signs. The author may want a sentence warning a reader with AWS variables set; it would be a citation of <<curl-protocols>>, not a case.
3. The `https` listener in the Solution is there for the shared-store claim; the Discussion says the Solution does not need it.
4. "surl warns on every start" for `--allow-anonymous` is cited, not a case: verify cannot read a server's log.
