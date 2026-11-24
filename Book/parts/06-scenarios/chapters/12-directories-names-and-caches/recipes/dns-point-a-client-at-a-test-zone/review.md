<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-point-a-client-at-a-test-zone
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 7 of 7 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. curl has no way to send plain DNS to a chosen server (`--dns-servers` is exit 2 in this build), so the recipe uses DoH: `--dns-transport https` on surl, `--doh-url` on curl.
3. A DoH lookup that fails certificate checks is exit 6, not 60; stated as a trap. `--doh-insecure` (loosening) has its case beside `--cacert`.
4. RFC 8484 (DoH) and RFC 6761 (.test) are not in the bibliography; the recipe cites surl-readme and curl-manual instead and claims .test only through the without-doh-url case.
