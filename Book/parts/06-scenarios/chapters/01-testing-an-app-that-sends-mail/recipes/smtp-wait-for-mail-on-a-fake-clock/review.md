<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-wait-for-mail-on-a-fake-clock

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 4 of 4 cases pass on windows (C#).

## Findings

1. Fitness (4): the fake clock is hand-written (about 60 lines) because no testing package is pinned; the Discussion says FakeTimeProvider from Microsoft.Extensions.TimeProvider.Testing drops into the same property, which was not run.
2. Truth (8): still waiting at 0 s and 29 s of fake time, TimeoutException at 30, under ten real seconds, wrong recipient and refused password are cases; the real CancellationToken (20 s) is only a guard.
3. The program returns 1 on a timeout on purpose: that is the failing test the recipe is about.
