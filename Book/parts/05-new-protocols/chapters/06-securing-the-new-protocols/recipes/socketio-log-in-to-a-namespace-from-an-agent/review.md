<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socketio-log-in-to-a-namespace-from-an-agent
Verdict: draft (written and self-checked by the recipe writer; not yet independently reviewed)
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 8 of 8 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; the loosening option is shown refused and then accepted.
3. Not covered: Linux and macOS were not run here; this protocol is client-first, so the TLS-before-greeting fault (Curl BL-5750) is not expected.
4. Known fault as in the console recipe (BL-1589).
