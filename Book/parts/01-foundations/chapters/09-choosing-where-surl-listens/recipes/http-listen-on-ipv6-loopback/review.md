<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-ipv6-loopback
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 11 of 11 cases pass on windows.
## Findings
1. The plan said curl needs -g for the bracketed address. It does not: curl leaves an IPv6 host alone, with or without -g. -g matters only for brackets in the path; the recipe says so and shows both.
2. verify's ready: port waits on the IPv4 port only, so the [::1]-only server uses ready: none (a 0.2 s pause).
3. Not checked on Linux or macOS here; CI runners have an IPv6 loopback, which the recipe assumes.
