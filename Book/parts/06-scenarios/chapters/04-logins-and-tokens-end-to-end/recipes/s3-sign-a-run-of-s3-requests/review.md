<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-sign-a-run-of-s3-requests
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. curl's s3:// scheme has no DELETE (exit 4, refused by curl); the delete goes over https:// with --aws-sigv4 aws:amz:us-east-1:s3, which surl's s3 listener accepts (204). Stated in the Discussion and cased.
2. Error documents carry a random RequestId, so those cases match the Code only.
Re-pinned to Surl a423807a5d / Curl 0680b97577: curl's s3:// now sends -X DELETE (Curl s3 DeleteObject); the exit-4 case became a wrong-secret delete (403, exit 22) and a signed delete (204, empty listing). Finding 1 no longer holds.
