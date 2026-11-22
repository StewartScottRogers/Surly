<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-seed-mail-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, `seed.cs`; every claim kept. Each scenario starts its own server, so the scenarios that need an earlier message seed and fetch it themselves.
2. `smtp-needs-a-login` now seeds one message first, so the client's message is UID 2 (was 3, after the earlier cases' two); the claim says "after the seeded one, as UID 2". `after_uid` is now explained in the Discussion.
3. The SMTP-delivered message's `size` (208, with the headers the server adds) appears in that case's exact output but is not claimed.
4. Not claimed (observed earlier): `seed_mail` matches the account by the local part only, case-insensitively. Left for the author to judge.
5. `uid_validity` and `internal_date` vary per run; the program does not print them.
6. Bounded wait before reading the server's record (CI race sweep).
