<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-see-why-mail-was-refused-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 of 11, with surl-mcp 1.0.0, curl-mcp cases pass on windows.
## Findings
1. surl-mcp keeps no log, so surl's reason lines are not available to an agent; the recipe explains from get_exchanges (command, reply) and login_judged.
2. Same title as the console recipe in plan.yml.
