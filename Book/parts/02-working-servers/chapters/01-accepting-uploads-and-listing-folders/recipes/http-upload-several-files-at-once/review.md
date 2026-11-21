<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-upload-several-files-at-once
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.
## Findings
1. The task's -T "{a,b,c}.txt" works as asked; HTTP never lists a folder in surl, so the listing is taken over a ws:// listen URL on the same --directory (case http-does-not-list).
2. The solution server uses --allow-anonymous (loosening); the account case (no-login, with-login) and uploads-off are its refusals.
3. curl --digest over ws:// does not retry with Digest (401), and Basic over ws is 403, so a logged-in listing over ws is not possible on plain ws; not claimed.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 409, 401 and 405 refusals now carry a one-line body (BL-1444), printed before each -w line; three cases and the Discussion restated.
