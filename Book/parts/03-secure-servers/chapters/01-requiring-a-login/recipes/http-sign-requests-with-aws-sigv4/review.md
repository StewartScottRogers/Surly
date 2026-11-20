<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-sign-requests-with-aws-sigv4

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.

## Findings

1. `--auth aws-sigv4` alone answers 403 to every refusal (no challenge); without `--auth` the same refusals are 401 with Digest challenges. Both are cases. Surl's reason (a 401 needs a challenge, ADR-0032) is stated, not a case.
2. surl does not check the region or service of the signature scope (eu-west-1/ec2 accepted); a case, stated as observed.
3. The bibliography has no SigV4 entry; the recipes cite `aws-s3-api`. Not edited here.
