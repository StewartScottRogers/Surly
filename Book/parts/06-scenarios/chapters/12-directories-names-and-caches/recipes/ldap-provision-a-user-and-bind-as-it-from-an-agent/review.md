<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-provision-a-user-and-bind-as-it-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 8 of 8 windows cases pass; 2 cases are linux and macos only, left to CI.

## Findings

1. Same stand-in: add_ldap_entries and add_account play the job; records read with wait_for_event (timeout_ms 10000).
2. read_directory's text block is JSON {"ldif": ...}, not bare LDIF; the program reads StructuredContent.ldif.
