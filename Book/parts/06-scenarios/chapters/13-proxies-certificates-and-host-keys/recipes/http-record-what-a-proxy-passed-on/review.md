<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-record-what-a-proxy-passed-on

Verdict: accept

Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0 / Curl.Api 0.2.0 from the p7 feed; windows only.

## Findings

1. Program finding (reported to the lead, batch 14): with a port-0 proxy listen URL, a request through the proxy to its own bound port is forwarded to itself (second hop origin form, 400) instead of 403 NotAllowed, which a fixed-port proxy gives. The recipe uses a loopback port with nothing listening (502) instead.
