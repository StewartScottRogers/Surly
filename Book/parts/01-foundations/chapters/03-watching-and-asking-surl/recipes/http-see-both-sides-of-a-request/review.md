<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-see-both-sides-of-a-request
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. The 405 is a PUT with a good Digest login to a surl without --allow-uploads, so the refusal is not a login problem; the no-login (401) and --allow-uploads (201) cases bound it.
2. curl's own trace keeps the Digest Authorization header; surl's writes `Digest ****`.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 401 and 405 refusals now carry a short text body (BL-1444); solution and no-login send it to the null device with -o.
