<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-look-up-a-name
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 14 of 14 cases pass on windows.
## Findings
1. Fixed: two surl processes cannot share a `--directory` (`surl: (124) Directory zones is in use by another surl process`), so the zone-transfer server reads its own copy of the zone from `transfers/` (a YAML anchor keeps the two fixtures identical). The Discussion says so; the exit 124 itself is not claimed.
2. Readiness: DNS is UDP, but a `dns` listen URL also serves TCP on the same port by default, so `ready: port` (a TCP connect) works and `ready: none` with a sleep is not needed. The `over-tcp` case shows TCP is served.
3. Reader (Machine 12): curl writes a random message ID in the header line, so every case uses `stdout-matches`; the solution's pattern is the whole dig-like answer with `id: \d+`, which the book prints as a regular expression. Readable, but not pretty; an exact `stdout` is impossible without a fixed ID.
4. Not claimed: `CURL_EXTENSIONS=0` (exit 1, `Protocol "dns" not supported`) works in a probe, but a case cannot set an environment variable in a way both cmd.exe and the POSIX shell run, so it is left out.
5. Not claimed: the encrypted transports (`--dns-transport tls|quic|https`), the TTL of the SOA record in negative answers (300, the SOA MINIMUM; RFC 2308 is not in the bibliography), and the `ra` flag's absence beyond what the exact flags line shows.
6. Platform: on this Windows machine, ranges of UDP ports are reserved (`netsh int ipv4 show excludedportrange protocol=udp`); a free TCP port from verify can fall in one, and surl then exits 45 (`Could not bind ... Permission denied`). verify retries once on fresh ports. Seen twice in manual probes, not in verify runs.
