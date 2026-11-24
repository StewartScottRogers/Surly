<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-resolve-names-over-tls
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Program finding: curl's `--dns-over-tls host:port` resolver trusts only the system store (`--cacert` and `-k` ignored), so it cannot be used against a test certificate; the recipe resolves with `dns://` + `--dns-transport tls --cacert` and calls with `--resolve`, via a short sh script. The limitation is a case (exit 6).
3. The question line of curl's dig text has `A` as field 4 with an empty field 5; the script parses answers with `NF == 5`.
4. Exit 60's message is Schannel text on Windows, so only the exit code is claimed. RFC 7858 is not in the bibliography; surl-readme is cited.
