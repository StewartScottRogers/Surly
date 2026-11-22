<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-fail-a-test-on-mail-to-another-domain-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 9 of 9, with dotnet 11, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core cases pass on windows.
## Findings
1. surl-mcp has no log tool, so the console's log-reason claim is carried as a scan of get_exchanges for RCPT TO answered 550, after wait_for_event exchange_ended per send.
