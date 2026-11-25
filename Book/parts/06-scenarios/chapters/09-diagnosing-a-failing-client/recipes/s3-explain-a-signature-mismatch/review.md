<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-explain-a-signature-mismatch
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 3 of 3 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The solution's client is a hand-rolled SigV4 signer (in the program) that signs the path-style path; curl's own signing never mismatches here. The agent re-signs with the known secret under three readings.
3. Not claimed: surl accepts a credential scope in another region (eu-west-1) with no error, and its SignatureDoesNotMatch body carries no CanonicalRequest/StringToSign as AWS's does (reported).
