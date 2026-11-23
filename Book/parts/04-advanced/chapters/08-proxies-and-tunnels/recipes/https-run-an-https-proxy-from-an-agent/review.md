<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-run-an-https-proxy-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), curl 8.22.0 (Curl 2145a49eae), the bin6 builds of env.sh; windows only.

## Findings

1. curl-mcp transfer now has proxyCaFile and proxyUser (fact sheet said it had none); no proxy-insecure field, so the curl tool is used for that case.
