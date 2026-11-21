<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-share-one-account-file-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.

## Findings

1. get_events login_judged shows one accepted login per protocol for alice and refusals under the claimed name; a bad file is a start_server tool error with exit_code 2.
