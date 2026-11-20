<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: xmpp-ask-what-a-server-offers
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 12 of 12 cases pass on windows.
## Findings
1. Plan task changed, nearest honest recipe: the task asks for disco info and items. The Curl port's XMPP client asks disco#info only: `?disco;items` prints the info answer, and `?items` is exit 3 `Unsupported XMPP query type: items`. The items answer (an empty `query`) is shown in the C# twin by a client that writes the stanzas.
2. Program finding (known, xmpp-send-a-message review 2a): curl without `-u` against a server with `--allow-anonymous` exits 67 `invalid-mechanism`, not a login; this recipe's server has accounts and the `no-login` case claims only the no-`ANONYMOUS` message.
3. Disco is asked after the login, not before as the task says; the Discussion and the case `no-login` say so. Bibliography: XEP-0030 (service discovery) is not in it, so the Discussion names the namespaces and cites RFC 6120 only; adding XEP-0030 would let it cite the rule.
4. `bob@127.0.0.1` is `service-unavailable` (an account with no bound stream), not an `account` identity; with `--mail-domain chat.example` both `chat.example` and the listen host answer as the server, and `alice@chat.example` was `service-unavailable` while alice's stream was bound to the other domain (not claimed).
