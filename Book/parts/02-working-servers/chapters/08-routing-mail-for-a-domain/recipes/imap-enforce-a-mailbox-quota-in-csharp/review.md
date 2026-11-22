<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-enforce-a-mailbox-quota-in-csharp
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 of 11, with dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0 cases pass on windows.
## Findings
1. The console's APPEND refusal is not carried: Curl.Api still cannot IMAP APPEND at Curl c84035f797 (byte body or CurlContent.FromStream with a length: CurlException 25 'Cannot APPEND with unknown input file size'). DeliverAsync's QuotaExceeded refusal stands in. Report to Curl (BL-5610 not in this build).
2. Every read of the record waits for ExchangeEnded (10 s bound).
