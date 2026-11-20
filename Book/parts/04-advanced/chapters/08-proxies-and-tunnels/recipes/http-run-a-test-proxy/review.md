<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-a-test-proxy

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. All claims are cases; one surl holds origin and proxy (`--proxy` beside a bare listen URL).
2. The allowed-origin case accepts 504 or 502: 504 on Windows (192.0.2.1 silent, `--head-timeout 2`); Linux/macOS CI may refuse at once (502). The claim states both.
3. `CONNECT` refused by the proxy is curl exit 7 (`CONNECT tunnel failed, response 403`), not 56; matches upstream curl 8.
4. Program note, not a defect: `localhost` as origin took about 5 s through the proxy (IPv6 first?); the recipe uses 127.0.0.1.
