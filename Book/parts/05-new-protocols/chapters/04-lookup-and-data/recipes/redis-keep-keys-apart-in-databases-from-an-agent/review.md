<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-keep-keys-apart-in-databases-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows (agent).

## Findings

1. Twin of <<redis-keep-keys-apart-in-databases>>: carries its claims as far as the agent surface can show them, each in its own words; see that recipe's review for the findings that apply to all three.
2. Truth (7): the loosening --allow-plaintext-auth has its refusal (WRONGPASS, exit 67) on the strict server and NOAUTH without a login; both are cases.
