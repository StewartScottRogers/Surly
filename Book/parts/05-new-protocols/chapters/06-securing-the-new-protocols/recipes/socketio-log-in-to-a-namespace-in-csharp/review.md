<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socketio-log-in-to-a-namespace-in-csharp
Verdict: draft (written and self-checked by the recipe writer; not yet independently reviewed)
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 8 of 8 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; the loosening option is shown refused and then accepted.
3. Not covered: Linux and macOS were not run here; this protocol is client-first, so the TLS-before-greeting fault (Curl BL-5750) is not expected.
4. Known fault as in the console recipe (BL-1589). 5. Curl.Api SendAsync does not return while a Socket.IO session is open, so the C# twin shows the join by the timeout (28) and the server's LoginJudged event, not the echoed event.
Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: SendAsync now returns at the first event, with it as the Body; the program prints the event and the LoginJudged verdict instead of catching code 28. Token account: two refusal cases (token as UserName, token as Password), as the console. 9 of 9 pass on windows.
