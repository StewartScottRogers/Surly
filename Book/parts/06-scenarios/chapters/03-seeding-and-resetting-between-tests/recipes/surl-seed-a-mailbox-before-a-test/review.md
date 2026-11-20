<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-seed-a-mailbox-before-a-test
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 10 of 10 cases pass on windows.
## Findings
1. seed: delivers the ten messages over SMTP in one curl command with a -T glob (order[01-10].eml), after the server is ready and before the Solution.
2. Cases share one store and run in order (read, seen, delete, renumbering); the order is part of the story and noted in recipe.yml.
3. Failure paths: seeding without a login (MAIL 530, curl 55) and a wrong password (curl 67). The 55 case runs last but one, so a regression that accepted the mail could not disturb the earlier cases.
