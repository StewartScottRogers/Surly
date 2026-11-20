<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-listen-on-ipv6-loopback-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. No program finding. A bare IPv6 listen URL is SurlServerStartException MalformedUrl (3); a bare one in a request is CurlException UrlMalformat (3).
2. Two listen URLs with port 0 get two different ports, so unlike the console recipe the IPv4 and IPv6 URLs differ in port.
3. Not checked on Linux or macOS here; CI runners have an IPv6 loopback, which the recipe assumes.
