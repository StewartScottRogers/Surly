<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-refuse-an-oversized-request-head
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. The plan says 431; 431 is RFC 6585's, which is not in the bibliography, so the recipe names no RFC for it. Add rfc6585 to references/bibliography.adoc to cite it.
2. Big cookies come from fixture files through `curl -H @file`, so the command line stays short; the 1,200-byte fixture makes recipe.yml one long line.
3. The 1 KiB boundary itself is not claimed: the head's length depends on the port in Host, so the cases keep a margin (600 and 1,200 bytes).
4. The 100k default and "0 means no limit" are claimed from `surl --aihelp limits`, not by sending a 100 KiB head (fixture too large); the C# twin sends 200,000 bytes and gets 431.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 431 now carries a 36-byte text body, `431 Request Header Fields Too Large` (BL-1444); the cases and the Discussion say so.
