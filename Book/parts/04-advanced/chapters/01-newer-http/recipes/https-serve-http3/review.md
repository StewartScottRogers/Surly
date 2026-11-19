<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-http3
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 11 of 11 cases pass on windows.
## Findings
1. Platform: this Curl port's `curl -V` lists `HTTP3` (Schannel build) and HTTP/3 works on Windows against `surl --http3`, certificate verified with `--cacert`; cased (`curl-has-http3`, solution, `untrusted-certificate`).
2. Readiness: `ready: port` waits for the TCP listener only. With `-v`, `surl` logs `Serving HTTP/3 over QUIC on UDP port` before `Listening on`, so the UDP listener is open first; that is a log line verify cannot see, so it is in this comment and in the recipe.yml comment, not the Discussion. All cases passed in the last two full runs (`-j 3`); the only failures in the first were the two claims removed below.
3. Removed, not deterministic: "`curl --http3` gets HTTP/3 here". `--http3` races QUIC against TCP, and under `verify.py -j 3` the TCP attempt won (answer `2`) while alone QUIC won (`3`, about 0.2 s to connect). The Discussion keeps only the deterministic half: against a server with no UDP listener, `--http3` falls back to HTTP/2.
4. Removed, program finding: `curl --alt-svc <file>` records the `h3` entry but never uses it, on a reused connection, on a new command, with `--http2`, `--http1.1` or no version option; only an explicit `--http3` gets HTTP/3. `surl --aihelp http` says curl acts on it. The Discussion claims only the header and its absence without `--http3`.
5. Fixed: "off by default because it opens a second UDP port" was a guess at the reason and was cut; the Discussion states the two refusals at start (no `https` URL, `--tls-max` below 1.3), both cased with exit 2.
6. Exit 56 for `--http3-only` against no UDP listener is what this build gives on Windows; upstream curl may give 7 or 28 on other platforms. Worth checking on Linux and macOS before publishing.
