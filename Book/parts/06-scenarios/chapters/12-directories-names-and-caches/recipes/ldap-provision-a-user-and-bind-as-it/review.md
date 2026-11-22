<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-provision-a-user-and-bind-as-it

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 8 of 8 windows cases pass; 2 cases are linux and macos only, left to CI.

## Findings

1. Nearest honest recipe: surl refuses an LDAP add over the wire (unwillingToPerform), so the test stands in for the job through the control endpoint (POST /v1/directory, POST /v1/accounts); said in the Discussion.
2. surl checks a bind against its accounts, never the entry's userPassword: the job must add both; a case shows the entry alone cannot bind.
3. A refused bind is judged twice per connection on Windows (WinLDAP retries); no count of refused events is claimed.
4. Every read of the record uses GET /v1/events/wait (first matching event, bounded by timeout_ms).
