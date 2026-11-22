<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: xmpp-ask-what-a-server-offers-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. `get_exchanges` records the wire text of an `xmpp://` exchange after STARTTLS, so the agent can show the one IQ curl sent (`id="disco"`, namespace `disco#info`) for `?disco;items`. That is the case `items-not-asked`; the exchange numbers are not claimed.
2. The `untrusted` case prints `certificate not trusted` for exit 60 because the TLS library's text differs by platform (schannel here).
3. Left out on purpose: the fixed `id` `disco` is not claimed beyond the case that prints it.
4. Bounded wait before reading the server's record (CI race sweep).
