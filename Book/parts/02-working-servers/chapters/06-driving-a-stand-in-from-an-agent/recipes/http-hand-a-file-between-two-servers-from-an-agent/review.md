<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-hand-a-file-between-two-servers-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 7 cases pass on windows (verify.py --no-write).
## Findings
1. put_file records two store_changed events on the second server (the /handed folder and the file), both change seeded; seeding at start_server records none on the first.
2. A 300-byte non-text file passes as base64 from read_file to put_file; transfer answers it base64 and the program compares bytes.
3. Upload into a missing folder is 409, so the first server is seeded with in/README.txt.
