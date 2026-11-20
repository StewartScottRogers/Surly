<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-refuse-clients-below-tls-1-3-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 3 of 3 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; loosening options are shown refused where named.
3. Fitness (3): curl-mcp's transfer has minTlsVersion and caFile but no maximum TLS version, so the agent drives the curl tool for both clients; stated in the Discussion.
- Platform (2026-11-19, CI run 44662810792): solution and default-range-takes-1-2 are [windows, linux]; new [macos] case shows the TLS 1.3 client exit 35 against the default range. Prose states it.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: curl-mcp transfer gained minTlsVersion and caFile (BL-5596) but has no maximum version, so it cannot be the TLS 1.2 client; the agent keeps the curl tool for both clients, and finding 3 and the Discussion now say why. 3 of 3 pass.
