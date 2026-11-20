<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-a-pfx-certificate-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 2 of 2 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; loosening options are shown refused where named.
3. Truth (7): the messages for a wrong passphrase, a missing one and a file that is not a PFX now differ; the Discussion quotes each it shows. Not covered: DER, a PFX with an empty passphrase, a PFX chain.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: surl's P12 refusals now name a wrong or missing --pass (BL-1445); claims and messages rewritten, the "message does not say" text dropped.
