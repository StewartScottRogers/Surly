<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-move-a-client-to-http3
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 6 of 6 windows cases pass; the [linux, macos] case is skipped here and left to CI.
## Findings
1. Platform: with no version option, Windows curl never acts on an `h3` Alt-Svc entry (Curl ADR-1313, as the Schannel build, which has no HTTP/3), so the planned switch happens only on Linux and macOS (ADR-1309). The Solution is `platforms: [linux, macos]`, not probed locally; a `[windows]` case shows the stay on HTTP/2, and `--http3` with the entry is HTTP/3 everywhere. For the author: this port's Windows build does list HTTP3, so a Windows reader cannot get the browser-like switch at all; ADR-1313 may deserve a second look.
2. The second request races QUIC and TCP; `--happy-eyeballs-timeout-ms 4000` is only for determinism, and the Discussion says so.
3. No bibliography entry for Alt-Svc (RFC 7838); RFC 9114 is cited for it.
4. The https-serve-http3 finding (Curl bug report 2026-11-19 #1, `--alt-svc` never used) is on Windows the designed behaviour of ADR-1313.
5. Revised after the Curl session (ADR-1313 stands; with --http3 a stored h3 entry is used on Windows too): the solution passes --http3 (Http3) and runs on all three platforms; the no-version-option switch is a [linux, macos] case and the stay on HTTP/2 a [windows] case.
