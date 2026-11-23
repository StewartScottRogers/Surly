<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-explain-why-mail-never-arrived
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 6 of 6 cases pass on windows.
## Findings
1. Distinct refusals from smtp-see-why-mail-was-refused-from-an-agent: the size limit (`--max-filesize 2k`: EHLO `SIZE 2048`, MAIL FROM SIZE=2761 answered 552 5.3.4), a refused recipient under --mail-rcpt-allowfails (exit 0), and accepted-then-discarded.
2. "Never arrives" is a bounded wait_for_event (5 s, timed_out) plus list_mailboxes' refusal for the owner.
3. The messages are written by the program with explicit CR LF, so their size is the same on every platform.
