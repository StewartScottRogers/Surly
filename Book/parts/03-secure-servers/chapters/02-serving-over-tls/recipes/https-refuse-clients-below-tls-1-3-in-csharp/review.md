<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-refuse-clients-below-tls-1-3-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 3 of 3 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; loosening options are shown refused where named.
3. Not covered: --tlsv1.0 and --tlsv1.1 (loosening, cited only), the exact curl wording of the failed handshake (library dependent).
- Platform (2026-11-19, CI run 44662810792): solution and default-range-takes-1-2 are [windows, linux]; new [macos] case shows the TLS 1.3 client refused 35 by the default range. Prose states it.
