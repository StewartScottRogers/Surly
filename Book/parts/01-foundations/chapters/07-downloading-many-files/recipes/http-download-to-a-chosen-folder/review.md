<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-to-a-chosen-folder
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 8 of 8 cases pass on windows.
## Findings
1. Written for batch-01; every Discussion claim is a case, except overwriting, which is a case and also cited (curl-manual).
2. -O on a URL ending in a slash saves under the folder name (/docs/ -> docs), and only the bare root gives curl_response; the recipe claims what was observed.
3. Exit 23's message is 'client returned ERROR on write of N bytes'; the case checks only 'curl: (23)'.
4. Re-pinned to Surl a423807a5d / Curl 0680b97577: a 404 saved with -o now holds surl's body, 404 Not Found (BL-1444); the no-name case checks the two saved files too.
