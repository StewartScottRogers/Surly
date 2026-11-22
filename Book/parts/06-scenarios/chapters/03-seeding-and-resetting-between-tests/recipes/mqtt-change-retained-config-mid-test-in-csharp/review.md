<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-change-retained-config-mid-test-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 of 8 cases pass on windows (C#).

## Findings

1. Twin of <<mqtt-change-retained-config-mid-test>>: carries its claims as far as the C# surface can show them, each in its own words; see that recipe's review for the findings that apply to all three.
2. Truth (7): --allow-anonymous refusal is the closed server (exit 8, got 0005); retain is the --topic filter, and noretain and outside-the-filter cases show nothing kept.
3. Bounded wait before reading the server's record (CI race sweep).
