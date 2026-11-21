<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-seed-a-directory-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.

## Findings

1. add_ldap_entries answers entry_count; refusals are tool errors; read_directory is LDIF without userPassword.
Re-pinned to Surl 5da5b3eaf / Curl c84035f797: the @* rewrite had left program.cs with CRLF line endings, so `First.Split("\n\n")[2]` threw IndexOutOfRangeException; the file is LF again and the alice entry is taken with `IndexOf("dn: cn=alice")`, which holds for either ending. MCP SDK under @* fine.
