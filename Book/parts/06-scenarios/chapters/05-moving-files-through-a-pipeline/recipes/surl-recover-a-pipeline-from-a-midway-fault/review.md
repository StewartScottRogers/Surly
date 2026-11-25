<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-recover-a-pipeline-from-a-midway-fault
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 9 of 9 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The cut is drop;scheme=s3;after-bytes=1;close=graceful: an S3 put's answer is only a head, so curl exits 8 (not 18) and the object is already stored.
3. ready: none plus a seed wait on the HTTPS listener: verify's port check would spend one of the after-bytes rule's times (a connection that sends nothing spends it; reported to Surl).
4. Per-transfer --retry after --next is the resume; --retry alone does not retry 8.
