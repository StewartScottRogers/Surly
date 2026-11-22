<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-expire-a-token-mid-session-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 7 of 7 cases pass on windows.

## Findings

1. Same registry design; ResetAsync revokes. Records: WaitForEventAsync per login with AfterSequence, then FindEvents.
2. The console's empty-user control refusal has no in-process twin (Surl.Api is not the control endpoint); left out.
