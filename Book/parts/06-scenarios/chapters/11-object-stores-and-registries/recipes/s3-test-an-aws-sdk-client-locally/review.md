<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-test-an-aws-sdk-client-locally
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. AWSSDK.S3 4.1.2 added to build/packages.yml (the only non-Surly package so far).
3. Finding for Surl: the SDK's default PutObject (aws-chunked, STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER, CRC32 trailer) is 501 NotImplemented; the recipe sets RequestChecksumCalculation.WHEN_REQUIRED and claims the 501 as this build's behaviour (case default-checksums). Revisit when surl takes aws-chunked uploads.
4. Plain HTTP (--s3-plaintext) only; the SDK over TLS with a test certificate not tried.
