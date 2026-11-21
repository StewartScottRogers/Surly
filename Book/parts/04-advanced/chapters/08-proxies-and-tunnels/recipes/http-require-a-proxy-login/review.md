<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-require-a-proxy-login

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.

## Findings

1. Self-review against REVIEW.md. Basic over the http proxy listener is 403 unchecked; over the https listener the 407 offers Basic too and accepts it; --allow-plaintext-auth case beside the refusal.
2. Proxy and origin accounts kept apart, both directions (407 at the proxy, 401 at the origin).
3. No redaction claimed: until Surl BL-1476 a -v log of a --proxy listener withholds nothing; the Discussion says to treat it as a secret.
