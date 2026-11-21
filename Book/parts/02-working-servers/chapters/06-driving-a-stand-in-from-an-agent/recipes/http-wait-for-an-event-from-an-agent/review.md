<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-wait-for-an-event-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 9 of 9 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, `wait.cs`; every claim kept, one added.
2. Added `wait-first`: a C# program can send `wait_for_event` before the upload and await it after, which the call list could not. It passes, but would also pass if the event were kept before the wait arrived, so it does not prove the wait blocked; the claim says only that it answers the change.
3. Events are printed as `name=value` without `sequence`, `at`, `client` and the byte counts, which vary per run; `listen_url` prints as `url` when it equals the server's bound URL. `the-whole-story` now matches the four events exactly, not a subset.
4. Paging with `after_sequence` is now possible (a variable, not a string substitution) but is not added; `max_events` shows the page.
5. Observed, not claimed: sequence numbers have gaps (1, 5, 7, 9); `login_judged` also answers `is_refused_by_fault` false. The Discussion says only "numbered", never "consecutive".
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 403 refusal now carries a short text body (BL-1444); the upload writes the body to reply.txt with -o so standard output holds only the status.
