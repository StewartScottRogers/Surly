<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: signalrs-log-in-to-a-hub-in-csharp
Verdict: draft (written and self-checked by the recipe writer; not yet independently reviewed)
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 8 of 8 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; the loosening option is shown refused and then accepted.
3. Not covered: Linux and macOS were not run here; this protocol is client-first, so the TLS-before-greeting fault (Curl BL-5750) is not expected.
4. The twin sends the negotiate request only, as https or http: Curl.Api 0.2.0 cannot send a hub call.
- Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: Curl.Api now sends a Body over signalr(s):// as a hub invocation (BL-5758), so the twin calls Echo through the login like the console (answers "hi"); refusals are now CurlException HttpReturnedError 22 naming the negotiate status (401, 403) instead of responses; finding 4 is fixed. 8 of 8 cases pass on windows.
