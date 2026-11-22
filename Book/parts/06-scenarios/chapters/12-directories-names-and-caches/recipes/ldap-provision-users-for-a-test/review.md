<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-provision-users-for-a-test

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 6 of 6 cases pass on windows.

## Findings

1. add_ldap_entries is all or none; reset_server empties a directory that started empty (search exit 39 after).
2. read_directory is read after wait_for_event store_changed for the last user (bounded).
