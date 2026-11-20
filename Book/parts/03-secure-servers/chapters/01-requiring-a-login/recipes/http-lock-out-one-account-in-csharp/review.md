<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-lock-out-one-account-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.

## Findings

1. The refusal is a fault rule, `--fault "refuse-login;account=bob"`; the console lifts it through the control endpoint (`--control`, `--control-token-file`, `DELETE /v1/faults/f1`), which the twins replace with `Faults.Remove` and `remove_fault`. The endpoint's token check is a console case only.
2. No case shows the start-up warning `--fault` writes (a server's stderr is not checked); the Discussion says only that the rule is for tests.
3. The planned task asks for a refusal 'until the test lifts it': the console does that with the control endpoint and with `times=1` (it lifts itself), both cases.
4. Twin of `http-lock-out-one-account`: it carries the console recipe's claims that this surface can show, each in its own words; a claim only the console can show is said so in the Discussion.
