<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-relay-through-a-socks-proxy

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.

## Findings

1. Origin listens on http://localhost so socks5h:// resolves localhost quickly; with the origin on 127.0.0.1 only, surl's connect to localhost took about 4 s on Windows (tries ::1 first).
2. socks5 vs socks5h shown with --resolve origin.test: curl resolves it for socks5, the server cannot for socks5h (05 04, HostNotFound).
3. rfc1928 and rfc1929 (SOCKS5, its username/password) are not in the bibliography; cited surl-readme and curl-manual instead. Lead may add them.
4. Not probed on Linux/macOS: http://localhost binding both 127.0.0.1 and ::1 (the socks5 --resolve case needs 127.0.0.1).
