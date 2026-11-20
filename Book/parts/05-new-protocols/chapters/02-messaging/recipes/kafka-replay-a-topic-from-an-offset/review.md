<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-replay-a-topic-from-an-offset
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 12 of 12 cases pass on windows.
## Findings
1. Truth: every sentence of the Discussion has a case. Removed 'a real broker keeps the log on disk' (no case) and the claim about `-1` as an offset (only `abc` is cased; `-1` is also exit 3 when probed).
2. Overlap with kafka-produce-and-consume, which already has offset=1, the end and past the end. This recipe is the restart story: the saved offset, a consume that returns only the newer records, the caught-up empty read, and `after-a-restart`, where a saved offset 6 against a restarted in-memory surl is error 1 (a reader's first surprise).
3. Fitness: `restart: [main]` empties the log, so the case shows the offset is refused rather than read as 0.
