<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-a-pfx-certificate-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 2 of 2 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; loosening options are shown refused where named.
3. Truth (7): a wrong passphrase and a damaged file give the same surl message; the Discussion says so. Not covered: DER, a PFX with an empty passphrase, a PFX chain.
