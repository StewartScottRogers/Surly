<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-tell-the-refusals-apart-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. Program finding: Curl.Api cannot put to `s3://` (see s3-map-a-bucket-to-a-folder-in-csharp), so the console's signed-put and unsigned-put refusals have no case here; the Discussion says so. The signed listing refused with AccessDenied stands in for the signed-but-not-allowed claim.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: Curl.Api now puts a body to s3:// as curl -T does (BL-5594), so the console's two put refusals are cases here (signed-put-refused, anonymous-put-refused; the open server gains AllowUploads as the console's has); finding 1 and the Discussion's 'only a GET' sentence are gone. 11 of 11 pass.
