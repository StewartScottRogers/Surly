<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gs-sign-with-an-hmac-key-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 of 13 cases pass on windows.

## Findings

1. `get_events` `login_judged` names a token's account `bearer token`, not the empty name `--user :TOKEN` gives; the claim reads the first five events only because the put refused before judging may add or omit events (an HMAC put with uploads off added none, the token put did).
2. `curl-mcp` `curl` with `-T` puts to `gs://` and signs with `-u` or `--oauth2-bearer`; every console claim has a case here.
