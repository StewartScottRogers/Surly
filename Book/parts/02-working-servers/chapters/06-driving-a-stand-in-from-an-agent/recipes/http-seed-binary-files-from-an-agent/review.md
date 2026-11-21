<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-seed-binary-files-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Finding (reported): a single put_file of 3 MiB of random bytes (about 4 MiB of base64) did not answer within 100 s, where a tool error would be expected; recorded in the facts sheet, not cased.
2. curl-mcp transfer truncates a body at 1 MiB (isTruncated true); the recipe shows it and fetches the whole file with curl -o.
3. Content-Type is by extension only (.zip application/zip).
