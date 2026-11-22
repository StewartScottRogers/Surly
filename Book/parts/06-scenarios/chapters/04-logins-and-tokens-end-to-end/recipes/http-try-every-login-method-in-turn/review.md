<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-try-every-login-method-in-turn

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp of the same commits: 5 of 5 cases pass on windows.

## Findings

1. Basic not in --auth over http:// with --allow-plaintext-auth is 401 with no LoginJudged event, while a wrong Digest password is a refused event: the record tells a refused method from a refused password.
2. Each curl call is followed by wait_for_event exchange_ended (bounded) before get_events.
