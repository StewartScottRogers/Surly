<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-share-a-folder-read-only-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 6 cases pass on windows (verify.py --no-write).
## Findings
1. Cases share one folder on disk, so the program deletes data/up.txt first; a rerun against an existing file would answer 204, not 201.
2. Without --allow-anonymous even reads are 200 (no account exists), only uploads are 401.
