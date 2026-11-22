<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-drain-a-maildrop-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows (agent).

## Findings

1. Carries all ten console claims; every curl tool call is followed by wait_for_event exchange_ended (one connection per run), so the store is read after QUIT. No sleep.
2. Finding: curl -o into a folder that does not exist is not curl exit 2 here but a tool error with the text An error occurred invoking 'curl'.; the case claims what the build does. No DELE is sent either way.
