<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-a-certificate-chain-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.

## Findings

1. Windows' Schannel needs `--ssl-no-revoke` for any real chain and caches intermediates it has seen, so the leaf-only server uses a second intermediate. Linux and macOS are not run locally: the `60` wording differs (cases match the exit code), and the `58` message is matched by its prefix.
2. `--cacert inter.pem` (trusting the intermediate alone) verifies on Windows; not claimed.
3. Twin of `https-serve-a-certificate-chain`: it carries the console recipe's claims that this surface can show, each in its own words; a claim only the console can show is said so in the Discussion.
