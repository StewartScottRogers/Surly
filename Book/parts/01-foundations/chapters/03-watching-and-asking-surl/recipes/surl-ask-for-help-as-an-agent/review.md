<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-ask-for-help-as-an-agent

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 10 of 10 cases pass on windows.

## Findings

1. Fixed: "every topic page has five sections" and "all writes every page" overclaimed; one topic is checked for the sections and amqp, exit-codes and xmpp for `all`, so the wording now says so.
2. Fixed: regexes no longer contain backticks (they broke the monospace in the generated claims table); a backtick in surl's Markdown is matched by `.`.
3. Needs the author: `protocols: []`, since the recipe is about surl itself. The schema allows it; the index may want a value such as `surl`.
4. Needs the author: the Solution's output is shown as "(output matching /.../)", since the overview is too long to print exactly. A truncated listing would read better; the build has no way to say that yet.
5. Observed, not a defect: `surl --help` shows `--aihelp <topic>`, but the topic is optional (no topic gives the overview, checked by the solution).
6. Updated 2026-11-19 for Surl commit 993aa7072: the 124 row of `surl --aihelp exit-codes` now also covers the folder of a writable `--share` and gives more ways out. Case `one-topic` now pins only the code, the name, the start of the meaning (`Another surl holds the data directory`) and the start of the next step (`Stop the other surl`), so later rewording of the row does not break it.
