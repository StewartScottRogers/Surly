<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-http2
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 9 of 9 cases pass on windows.
## Findings
1. Fitness (3): a real certificate (setup: `openssl`, `--cacert`) rather than `--self-signed` and `-k`, so the handshake whose ALPN the recipe shows is a verified one; the untrusted-certificate case (exit 60) shows a failed handshake says nothing about the version.
2. Fitness (3): `--http2` changes nothing over `https` (the `http2-is-the-default` case proves it); the Discussion says so and keeps it only so the command names its purpose.
3. Fixed: the Solution first used `-v` and a stderr check, which the generated block cannot show; it now prints `HTTP/%{http_version}` after the body, and the ALPN lines are their own cases (`server-accepts-h2`, `curl-offers-both`).
4. Truth (4): "`Upgrade: h2c` over `https` is ignored" is cased by sending the three headers by hand with `--http1.1` (curl itself never sends the upgrade over TLS). An earlier draft said the specification requires this; that was not a checked claim and was cut, citing the Surl README instead.
5. Reader: acronyms HTTP/2, TLS, ALPN spelled out; See Also points at the upgrade, prior-knowledge, HTTP/3 and certificate recipes.
