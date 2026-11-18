<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-receive-a-file-as-a-message
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 6 of 6 cases pass on windows.
## Findings
1. "Sent as one message and followed by a close" is cited to <<surl-readme>>; no case observes framing (curl prints only the payload).
2. Fixed: prose used `{{null}}`, which is not substituted outside recipe.yml; now `/dev/null` (`NUL` on Windows). Removed an unchecked "nothing is written".
3. The login sentence ("once an account is configured the upgrade needs one") is cited to <<surl-readme>>, not cased; it points to <<wss-log-in-over-tls>>.
4. Sources wanted for the bibliography: RFC 6455 (WebSocket). <<rfc9110>> covers 404, 405, 426.
5. The generated Solution block prints `Listening on http://127.0.0.1:8080/` for this non-HTTP listener: build.py hard-codes the scheme. Needs a build fix (lead); not editable from the chapter.
6. Verified after the byte-exact fixture fix; unaffected.
