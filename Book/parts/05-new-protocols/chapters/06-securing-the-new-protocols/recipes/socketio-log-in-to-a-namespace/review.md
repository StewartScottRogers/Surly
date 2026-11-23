<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socketio-log-in-to-a-namespace
Verdict: draft (written and self-checked by the recipe writer; not yet independently reviewed)
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 8 of 8 cases pass on windows.
## Findings
1. Truth (4): every claim in the Discussion is a case; certificates come from setup:, never fixtures.
2. Fitness (1): the Solution answers the Problem as stated; the loosening option is shown refused and then accepted.
3. Not covered: Linux and macOS were not run here; this protocol is client-first, so the TLS-before-greeting fault (Curl BL-5750) is not expected.
4. Known fault: a token account (--user :tok123) refuses the right token in this build (BL-1589, fixed in Surl master); the last case records the refusal and becomes an accepted login once the build has the fix.
Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: Curl now ends an emit at the first event sent back, so a join exits 0, not 28. BL-1589 does not reach curl: Surl takes a CONNECT {"token":..}, but curl -u always sends {"username","password"}, so neither -u tok123: nor -u :tok123 logs in to a token account (two refusal cases replace the known-bug case). Finding for Curl: no way to send a Socket.IO token. 9 of 9 pass on windows.
