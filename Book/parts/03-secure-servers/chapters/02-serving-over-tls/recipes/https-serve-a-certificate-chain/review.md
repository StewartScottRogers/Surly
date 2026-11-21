<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-a-certificate-chain

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.

## Findings

1. Windows' Schannel needs `--ssl-no-revoke` for any real chain and caches intermediates it has seen, so the leaf-only server uses a second intermediate. Linux and macOS are not run locally: the `60` wording differs (cases match the exit code), and the `58` message is matched by its prefix.
2. `--cacert inter.pem` (trusting the intermediate alone) verifies on Windows; not claimed.
