<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-audit-a-bucket-after-a-sync
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 cases pass on windows.
## Findings
1. The sync is one curl call with -T pairs; a missing local file stops it with exit 26 after the earlier puts, a tool error.
2. The audit reads the store with list_files (byte_count) and read_file; same-size changes are found only by reading. Manifest is the out folder the sync copies from, not a separate file.
3. Without --list-directories a client S3 listing is 403 AccessDenied, curl 22, while list_files still works.
