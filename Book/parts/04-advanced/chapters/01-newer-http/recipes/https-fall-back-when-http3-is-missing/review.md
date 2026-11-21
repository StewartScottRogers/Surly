<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-fall-back-when-http3-is-missing
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.
## Findings
1. Two kinds of blocked UDP: nothing listening (turned away: `--http3` falls back, `--http3-only` exit 56, which https-serve-http3 already passes in CI on Linux and macOS) and a swallowing listener (a `tftp://` URL on the same port number stands in; it logs each QUIC datagram as `opened: tftp`).
2. Program finding (Curl): `curl --http3-only --cacert server.pem https://127.0.0.1:P/hello.txt` with a non-QUIC UDP listener on P ran 300 s and exited 28 (`Connection timed out after 300019 milliseconds`); expected, per Curl ADR-0144's silent-peer row, exit 55 after about 10 s. The recipe uses `--connect-timeout 2` (28) and advises it.
3. `--happy-eyeballs-timeout-ms 4000` in the HTTP/3 contrast is only for determinism (the default race lost to TCP under load in https-serve-http3); the Discussion says so.
4. Linux and macOS results for the swallowed cases (0 and 28) are not yet seen in CI.
