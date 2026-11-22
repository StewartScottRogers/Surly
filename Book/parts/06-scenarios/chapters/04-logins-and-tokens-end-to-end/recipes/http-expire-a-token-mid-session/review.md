<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-expire-a-token-mid-session

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 8 of 8 cases pass on windows.

## Findings

1. Nearest honest recipe: a fixed Bearer account (--user :tok) cannot be revoked or replaced at run time (control POST /v1/accounts with an empty user is 400; no route names it), so the recipe uses surl's OCI registry token endpoint, which issues a new token per login; POST /v1/reset revokes every token. Said in the Discussion; protocols [https, oci].
2. The 'retry once' failure path uses --fault error;scheme=oci;path=/v2/;reply=401 UNAUTHORIZED (its 401 carries no WWW-Authenticate).
3. logins.sh polls /v1/events with a bound (25 x 0.2 s); token lifetime (300 s) is stated, not waited for.
