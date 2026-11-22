<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-sync-a-folder-to-a-bucket
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. `curl -T "site/{a.csv,b.csv,c.csv}" s3://reports/` puts each file under its own name; a missing name in the pattern is exit 26 after the earlier puts; a refusal is attempted for every file and ends exit 22.
2. The proof of "only one" is the log count (`-v --log-file`, `bytes stored`) and the signed ListObjectsV2 listing with `--http1.1` (Size and ETag = MD5 of the content). The log's `stored at` lines hold a disk path, so only the count is claimed.
3. The case order matters: the two log counts follow the puts they count.
