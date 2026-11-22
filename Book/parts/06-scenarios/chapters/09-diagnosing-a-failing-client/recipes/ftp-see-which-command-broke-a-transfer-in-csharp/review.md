<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-see-which-command-broke-a-transfer-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 8 cases pass on windows.
## Findings
1. Data-connection bytes are in the same Wire layer with no StreamId, so the program skips received chunks between a 1xx reply and the next reply.
2. `FaultAppliedEvent.Target` for FTP is the command and the path (`STOR /outbox/nightly.csv`).
