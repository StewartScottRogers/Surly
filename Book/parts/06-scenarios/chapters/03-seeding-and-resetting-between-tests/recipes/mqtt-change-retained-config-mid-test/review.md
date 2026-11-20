<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-change-retained-config-mid-test

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows (console).

## Findings

1. Fitness (3): the device's read is one curl subscribe; surl ends it after the retained messages and curl exits 0 (cited <<surl-readme>>, not a bug).
2. Truth (7): --allow-anonymous refusal is the closed server (exit 8, got 0005); retain is the --topic filter, and noretain and outside-the-filter cases show nothing kept.
3. The console restart case shows --directory keeps retained messages across a restart; without the fixture state/ surl exits 37 (Could not open directory), so the fixture makes it.
4. Surface difference, said in the twins: a seed through Surl.Api or surl-mcp sets RETAIN itself and is kept outside the filter; a client's publish is not. The console has no seed with that flag.
