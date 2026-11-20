<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: coap-get-a-resource
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 12 of 12 cases pass on windows.
## Findings
1. Readiness: CoAP is UDP only, so every server is `ready: none`. A confirmable request does not cover a server that has not bound yet: on Windows the ICMP port-unreachable ends `curl` at once with exit 7, before any retransmission. The first case, `wait-for-start`, uses `curl --retry 5 --retry-all-errors --retry-delay 1` (probed: works with `surl` started 3 s after `curl`), and `main` starts last. The same UDP port-exclusion risk as in `webtransport-open-a-stream` finding 3 applies (four UDP servers here); fix belongs in verify.py.
2. Fixed: `coaps` uses `--self-signed` and `curl -k` rather than an `openssl` setup line, which would be printed first in a Solution that does not use it. Probed separately: `--cert`/`--key` with `curl --cacert` also works over DTLS on Windows.
3. Secure by default: with an account, plain `coap` reads are `4.01` (NoSec carries no login, so `-u` changes nothing); `--allow-anonymous` serves them. Both cased.
4. Not claimed, curl limit: the Curl port's `coaps://` offers only certificate mode, no pre-shared key (Curl `Coap-Dtls-Remaining.md`, ADR-1078). Against `surl --user alice:secret coaps://...` with no certificate (PSK only), `curl -u alice:secret` and `curl -k` both exit 35. The Discussion says curl offers only certificate mode, citing <<curl-protocols>>; that `surl` accepts PSK is cited to <<surl-readme>> (its coap help), not cased.
5. Help text: `surl --aihelp coap` says a `coaps` URL "needs a certificate or an account", but the exit-58 message names only `--cert` and `--self-signed`. Minor; for the author.
6. `%{response_code}` is `000` after a CoAP transfer; `-i` and `-f` are used instead. `-I` on a coap URL sends a GET and prints the payload as well (CoAP has no HEAD); not used.
7. Not claimed: plain `coap` to a `coaps` port hangs until curl's own give-up (over 20 s); left out.
8. Updated 2026-11-19 for Surl 763754785 and Curl b9ca725077: the exit-58 message for a `coaps` URL with no certificate now names `--user <name:key>` for a pre-shared key as well as `--cert` and `--self-signed` (finding 5 fixed); the case pattern and the Discussion follow it. Curl's `coaps://` still offers no pre-shared key (re-probed: `-u alice:secret` against a PSK-only surl exits 35), so finding 4 stands.
