<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-tell-the-refusals-apart

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 14 of 14 cases pass on windows.

## Findings

1. Decision: the Solution is three transfers joined with `--next`, and `2>&1` puts the three messages together; its exit is 22 when the last fails. A one-line command per refusal is in the other cases.
2. `AccessDenied` is one code for unsigned, signed-but-uploads-off and signed-but-listing-off, with the same message; the Discussion says what tells them apart. `RequestTimeTooSkewed` is in aihelp and not claimed (it needs a clock 15 minutes off).
