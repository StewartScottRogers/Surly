<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-ipv6-loopback-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 5 of 5 cases pass on windows.
## Findings
1. No program finding. transfer answers info.primaryIp, the address that answered. A bare IPv6 request URL is InvalidRequest (no exit code) and a bare listen URL is exit_code 3 MalformedUrl.
2. surl-mcp refuses a [::] listen host as not loopback, like 0.0.0.0, before binding.
3. Not checked on Linux or macOS here; CI runners have an IPv6 loopback, which the recipe assumes.
