<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqp-bridge-to-amqp-1-0
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows.
## Findings
1. Truth: every sentence of the Discussion has a case; the AMQP 1.0 receive from an empty address waits (exit 28 under -m 2), which the recipe states and cases.
2. Fitness: --allow-plaintext-auth is the loosening; the locked server shows both refusals (SASL PLAIN not offered, CRAM-MD5 only).
