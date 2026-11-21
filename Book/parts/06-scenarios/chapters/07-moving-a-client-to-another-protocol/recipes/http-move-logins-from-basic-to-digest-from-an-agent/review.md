<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-logins-from-basic-to-digest-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 14 of 14 cases pass on windows.
## Findings
1. Carries the console claims through get_events login_judged and get_exchanges; adds the --allow-plaintext-auth warning from start_server log_lines. The verbose log is not reachable from an agent, so password-not-logged checks the events instead.
