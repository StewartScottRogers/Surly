<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-two-folders-as-shares

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 5 of 5 cases pass on windows.

## Findings

1. All five claims are cases and pass.
2. Probed: a second surl on a readonly share starts while the first serves it writable and readonly; a second on the writable share exits 124. The order readonly-held-first then writable-second was not probed, so the prose does not claim it.
