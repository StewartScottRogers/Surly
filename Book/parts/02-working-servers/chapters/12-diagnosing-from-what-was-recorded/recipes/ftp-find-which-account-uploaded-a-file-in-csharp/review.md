<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-find-which-account-uploaded-a-file-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 8 cases pass on windows.
## Findings
1. The plan asked for FindExchanges filtered by account and path; `FindExchanges` does not filter by `Name` (by the Surl source), so the finding uses `FindEvents` with `Account`, `Store` and `Name`, and `FindExchanges` by account shows the refused STOR. Noted for the lead.
2. The record is awaited by polling started against ended exchange events, 10 s bound.
3. The record keeps PASS in clear while the log withholds it; both are cases.
